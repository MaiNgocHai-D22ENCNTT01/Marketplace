from locust import HttpUser, task, between

class MarketplaceUser(HttpUser):
    wait_time = between(0.5, 1.5)

    @task(3)
    def browse_products(self):
        self.client.get("/api/products?page=1&pageSize=20")

    @task(1)
    def ai_recommendations(self):
        self.client.get("/api/products/recommendations?limit=8")
