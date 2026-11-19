DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM products p
        CROSS JOIN LATERAL (SELECT count(*) n, coalesce(round(avg(rating),2),0) avg_rating
                            FROM reviews WHERE product_id=p.id AND status='ACTIVE') t
        WHERE p.review_count<>t.n OR p.rating_avg<>t.avg_rating
    ) THEN RAISE EXCEPTION 'Incorrect product ratings'; END IF;

    IF EXISTS (
        SELECT 1 FROM shops s
        CROSS JOIN LATERAL (SELECT count(*) n, coalesce(round(avg(r.rating),2),0) avg_rating
                            FROM reviews r JOIN products p ON p.id=r.product_id
                            WHERE p.shop_id=s.id AND r.status='ACTIVE') t
        WHERE s.review_count<>t.n OR s.rating_avg<>t.avg_rating
    ) THEN RAISE EXCEPTION 'Incorrect shop ratings'; END IF;

    IF EXISTS (
        (SELECT customer_id,product_id,count(*) FROM reviews GROUP BY customer_id,product_id)
        EXCEPT ALL
        (SELECT user_id,product_id,count(*) FROM user_interaction_logs WHERE action_type='REVIEW' GROUP BY user_id,product_id)
    ) OR (SELECT count(*) FROM reviews) <> (SELECT count(*) FROM user_interaction_logs WHERE action_type='REVIEW')
    THEN RAISE EXCEPTION 'Missing or duplicate review interactions on fixture'; END IF;
END $$;
