"""Integration test in an isolated PostgreSQL cluster; requires local image cache."""
import argparse
import os
from pathlib import Path
import secrets
import socket
import subprocess
import tempfile
from decimal import Decimal

import psycopg2

ROOT = Path(__file__).resolve().parents[1]
AI = ROOT / "Marketplace.AIService"
import sys
sys.path.insert(0, str(AI))
import import_real_data as importer


def run():
    args = argparse.Namespace(cache=AI / "real_data_cache", webroot=ROOT / "Marketplace.WebAPI/wwwroot",
        offline=True, refresh=False, limit=2, max_images=4, usd_vnd=Decimal(25000),
        replace_demo=False, dry_run=False)
    catalog = importer.prepare_catalog(args)
    pg = Path(os.getenv("PG_BIN", r"C:\Program Files\PostgreSQL\17\bin"))
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        port = sock.getsockname()[1]
    with tempfile.TemporaryDirectory(prefix="marketplace-import-test-") as tmp:
        directory = Path(tmp)
        password = secrets.token_hex(24)
        pw = directory / "pw"
        pw.write_text(password, encoding="ascii")
        def command(*cmd, **kwargs):
            print(f"Running {Path(cmd[0]).name}", flush=True)
            # A background PostgreSQL process can inherit PIPE handles on Windows.
            # A real file avoids communicate() waiting for the server to exit.
            with tempfile.TemporaryFile() as output:
                result = subprocess.run(list(map(str, cmd)), stdout=output, stderr=output, timeout=90, **kwargs)
                output.seek(0)
                message = output.read().decode(errors="replace")
            if result.returncode:
                raise RuntimeError(message)
        command(pg / "initdb.exe", "-D", directory / "data", "-U", "postgres", "-A", "scram-sha-256",
                "--pwfile", pw, "--encoding=UTF8", "--locale=C")
        pw.unlink()
        command(pg / "pg_ctl.exe", "-D", directory / "data", "-l", directory / "postgres.log",
                "-o", f"-h 127.0.0.1 -p {port}", "-w", "start")
        previous = os.environ.get("MARKETPLACE_AI_DSN")
        try:
            os.environ["MARKETPLACE_AI_DSN"] = f"host=127.0.0.1 port={port} user=postgres dbname=postgres password={password}"
            env = dict(os.environ, ASPNETCORE_ENVIRONMENT="Development", SeedDemoData="true",
                ConnectionStrings__Marketplace=f"Host=127.0.0.1;Port={port};Database=postgres;Username=postgres;Password={password}")
            command("dotnet", ROOT / "Marketplace.WebAPI/bin/Release/net8.0/Marketplace.WebAPI.dll", "--migrate",
                    cwd=ROOT / "Marketplace.WebAPI", env=env)
            db = psycopg2.connect(os.environ["MARKETPLACE_AI_DSN"])
            db.autocommit = True
            with db, db.cursor() as cur:
                from psycopg2.extras import Json
                user, shop, category = (importer.uid(k) for k in ("test/user", "test/shop", "test/category"))
                importer.insert(cur, "users", id=user, email="import-test@example.invalid", password_hash="!disabled",
                                full_name="Test", status="INACTIVE")
                importer.insert(cur, "shops", id=shop, user_id=user, name="Test", status="ACTIVE")
                importer.insert(cur, "categories", id=category, name="Test", slug="test", status="ACTIVE")
                for n in range(2):
                    product, variant = (importer.uid(f"test/{k}/{n}") for k in ("product", "variant"))
                    importer.insert(cur, "products", id=product, shop_id=shop, category_id=category,
                                    name="Demo", base_price=10000, status="ACTIVE")
                    importer.insert(cur, "product_variants", id=variant, product_id=product, sku=f"DEMO-TEST-{n}",
                                    attributes=Json({}), price=10000, stock_quantity=10, status="ACTIVE")
                    if n == 1:
                        group, order = (importer.uid("test/" + k) for k in ("group", "order"))
                        importer.insert(cur, "order_groups", id=group, customer_id=user, total_amount=10000)
                        importer.insert(cur, "orders", id=order, order_group_id=group, shop_id=shop,
                                        shipping_address_snapshot=Json({}), total_amount=10000, order_status="COMPLETED")
                        importer.insert(cur, "order_items", id=importer.uid("test/item"), order_id=order,
                                        variant_id=variant, product_name_snapshot="Demo", variant_snapshot=Json({}),
                                        unit_price_snapshot=10000, quantity=1)
                cur.execute("SELECT count(*) FROM products")
                original = cur.fetchone()[0]
            def scalar(query):
                with db.cursor() as cur:
                    cur.execute(query)
                    return cur.fetchone()[0]
            args.dry_run = True
            importer.import_catalog(args, catalog)
            assert scalar("SELECT count(*) FROM products") == original
            args.dry_run = False
            importer.import_catalog(args, catalog)
            assert scalar("SELECT count(*) FROM products") == original + 2
            assert scalar("SELECT count(*) FROM product_images WHERE image_url LIKE '/images/real/%'") == 6
            assert scalar("SELECT count(*) FROM reviews WHERE comment LIKE '[Đánh giá mô phỏng]%'") == sum(
                2 + int(p["id"]) % 2 for p, _, _ in catalog)
            importer.import_catalog(args, catalog)
            assert scalar("SELECT count(*) FROM products") == original + 2
            orders = scalar("SELECT count(*) FROM order_items")
            args.replace_demo = True
            importer.import_catalog(args, catalog)
            assert scalar("SELECT count(*) FROM order_items") == orders
            assert scalar("SELECT count(*) FROM products") < original + 2
            with db.cursor() as cur:
                cur.execute("SELECT count(*) FROM pg_available_extensions WHERE name='vector'")
                has_vector = cur.fetchone()[0]
            if has_vector:
                # Fresh imports exercise writable ANN, generated ANN, and dimension rejection.
                with db.cursor() as cur:
                    cur.execute("CREATE EXTENSION IF NOT EXISTS vector")
                for generated in (False, True):
                    with db.cursor() as cur:
                        cur.execute("ALTER TABLE product_images DROP COLUMN IF EXISTS feature_vector_ann")
                        suffix = " GENERATED ALWAYS AS (feature_vector::vector(1024)) STORED" if generated else ""
                        cur.execute("ALTER TABLE product_images ADD COLUMN feature_vector_ann vector(1024)" + suffix)
                    extra = [(dict(p, id=int(p['id']) + (10000 if generated else 20000)), images, price)
                             for p, images, price in catalog]
                    importer.import_catalog(args, extra)
                    with db.cursor() as cur:
                        cur.execute("SELECT feature_vector_ann = feature_vector::vector(1024) FROM product_images WHERE id=%s",
                            (importer.uid(f"image/{extra[0][0]['id']}/0"),))
                        assert cur.fetchone()[0]
                with db.cursor() as cur:
                    cur.execute("ALTER TABLE product_images DROP COLUMN feature_vector_ann")
                    cur.execute("ALTER TABLE product_images ADD COLUMN feature_vector_ann vector(512)")
                try:
                    importer.import_catalog(args, catalog)
                except ValueError:
                    pass
                else:
                    raise AssertionError("Wrong ANN dimensions were accepted")
            print(f"PASS: rollback, import, rerun, protected orders, replace-demo; pgvector={bool(has_vector)}")
            db.close()
        finally:
            if previous is None:
                os.environ.pop("MARKETPLACE_AI_DSN", None)
            else:
                os.environ["MARKETPLACE_AI_DSN"] = previous
            command(pg / "pg_ctl.exe", "-D", directory / "data", "-m", "fast", "-w", "stop")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    run()
