"""Build first. Starts a temporary PostgreSQL cluster, WebAPI and WebClient on loopback."""
import os
import secrets
import socket
import subprocess
import sys
import tempfile
import time
from contextlib import ExitStack
from pathlib import Path
from urllib.error import URLError
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[1]


def free_port():
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        return sock.getsockname()[1]


def run():
    pg_bin = Path(os.environ.get("PG_BIN", r"C:\Program Files\PostgreSQL\17\bin"))
    pg = lambda name: str(pg_bin / name)
    with tempfile.TemporaryDirectory(prefix="marketplace-step3-") as tmp, ExitStack() as stack:
        directory = Path(tmp)
        cluster = directory / "pgdata"
        password = secrets.token_hex(24)
        pwfile = directory / "pw"
        pwfile.write_text(password, encoding="ascii")
        pg_port = free_port()
        subprocess.run([pg("initdb"), "-D", cluster, "-U", "postgres", "-A", "scram-sha-256",
                        "--pwfile", pwfile, "--encoding=UTF8", "--locale=C"], check=True)
        pwfile.unlink()
        subprocess.run([pg("pg_ctl"), "-D", cluster, "-l", directory / "postgres.log",
                        "-o", f"-h 127.0.0.1 -p {pg_port}", "-w", "start"], check=True)
        stack.callback(subprocess.run, [pg("pg_ctl"), "-D", cluster, "-m", "fast", "-w", "stop"])
        env = dict(os.environ, ASPNETCORE_ENVIRONMENT="Development", SeedDemoData="true",
                   ConnectionStrings__Marketplace=f"Host=127.0.0.1;Port={pg_port};Database=postgres;Username=postgres;Password={password}")
        def app(name, *args):
            folder = ROOT / ("Marketplace." + name)
            dll = folder / "bin/Release/net8.0" / ("Marketplace." + name + ".dll")
            return ["dotnet", str(dll), *args], folder

        command, cwd = app("WebAPI", "--migrate")
        subprocess.run(command, cwd=cwd, env=env, check=True)
        def start(name, ready):
            url = f"http://127.0.0.1:{free_port()}"
            command, cwd = app(name, "--urls", url)
            log = stack.enter_context((directory / (name + ".log")).open("w+", encoding="utf-8"))
            process = subprocess.Popen(command, cwd=cwd, env=env, stdout=log, stderr=subprocess.STDOUT)
            def stop():
                if process.poll() is None:
                    process.terminate()
                    process.wait(timeout=15)
            stack.callback(stop)
            for _ in range(120):
                if process.poll() is not None:
                    break
                try:
                    with urlopen(url + ready, timeout=2) as response:
                        if response.status == 200:
                            return url
                except (OSError, URLError):
                    time.sleep(0.25)
            log.seek(0)
            raise RuntimeError(log.read()[-6000:])
        with_ai = "--ai" in sys.argv
        if "--visual-load" in sys.argv:
            env["VisualSearch__RequestsPerMinute"] = "50000"
        if with_ai:
            ai_port = free_port()
            env["AiBaseUrl"] = f"http://127.0.0.1:{ai_port}"
            env["MARKETPLACE_AI_DSN"] = f"host=127.0.0.1 port={pg_port} dbname=postgres user=postgres password={password}"
            env["MARKETPLACE_TEST_API"] = start("WebAPI", "/api/status")
            log = stack.enter_context((directory / "ai.log").open("w+", encoding="utf-8"))
            ai = subprocess.Popen([sys.executable,"-m","uvicorn","main:app","--port",str(ai_port)],
                                  cwd=ROOT/"Marketplace.AIService",env=env,stdout=log,stderr=subprocess.STDOUT)
            def stop_ai():
                if ai.poll() is None:
                    ai.terminate()
                    ai.wait(timeout=15)
            stack.callback(stop_ai)
            for _ in range(100):
                try:
                    with urlopen(env["AiBaseUrl"]+"/docs", timeout=1): break
                except OSError: time.sleep(.2)
            else:
                log.seek(0)
                raise RuntimeError(log.read()[-3000:])
        if not with_ai:
            env["MARKETPLACE_TEST_API"] = start("WebAPI", "/api/status")
        env["ApiBaseUrl"] = env["MARKETPLACE_TEST_API"] + "/"
        env["MARKETPLACE_TEST_WEB"] = start("WebClient", "/Account/Login")
        if "--chat" in sys.argv:
            subprocess.run([sys.executable, str(ROOT / "tests/test_chat.py")], env=env, check=True)
        if with_ai:
            test = [sys.executable, str(ROOT / "tests/test_ai.py")]
            subprocess.run(test + ["--online"], env=env, check=True)
            if "--visual-load" in sys.argv:
                port = env["MARKETPLACE_TEST_API"].rsplit(":", 1)[1]
                subprocess.run([sys.executable, str(ROOT / "load_test.py"), "--port", port,
                                "--users", "10000", "--requests", "10000", "--connections", "128",
                                "--ramp", "30", "--images", str(ROOT / "Marketplace.WebAPI/wwwroot/images/demo"),
                                "--output", str(ROOT / "visual-load-windows.json")], env=env, check=True)
            stop_ai()
            subprocess.run(test, env=env, check=True)
        for suite in ("test_wishlist.py","test_step3.py","test_web.py","test_flash_sales.py","test_account.py"):
            subprocess.run([sys.executable, str(ROOT / "tests" / suite)], env=env, check=True)
        subprocess.run([pg("psql"), "-h", "127.0.0.1", "-p", str(pg_port), "-U", "postgres", "-d", "postgres",
                        "-v", "ON_ERROR_STOP=1", "-f", "-"], input=(ROOT / "tests/review_totals.sql").read_bytes(),
                       env=dict(env, PGPASSWORD=password), check=True)


if __name__ == "__main__":
    run()
