"""
Flash-sale load test for the Big Data lab.

Run from a student machine:

    pip install locust
    locust -f locustfile.py --host http://<vm-ip>:8080

Then open http://localhost:8089 in the browser, set the number of users
(start with 20, ramp up in steps) and a spawn rate of 2-5 users/s, and
watch the /buy latency climb while /products stays fast.

Reset the flash sale stock between runs (teacher):

    curl -X POST http://<vm-ip>:8080/admin/restock \
         -H "Content-Type: application/json" \
         -d '{"productId": 1, "stock": 250}'
"""
import random

from locust import HttpUser, between, task

FLASH_PRODUCT_ID = 1
NUM_CUSTOMERS = 5000  # must match the seeded customer range (1..5000)


class FlashSaleUser(HttpUser):
    wait_time = between(0.5, 2.0)

    def on_start(self):
        self.customer_id = random.randint(1, NUM_CUSTOMERS)

    @task(3)
    def browse_product(self):
        # Reads are cheap and should stay fast even under heavy load -
        # that contrast is part of the exercise.
        product_id = random.randint(1, 4)
        self.client.get(f"/products/{product_id}", name="/products/[id]")

    @task(1)
    def buy(self):
        with self.client.post(
            "/buy",
            json={"productId": FLASH_PRODUCT_ID, "customerId": self.customer_id},
            name="/buy (flash sale)",
            catch_response=True,
        ) as resp:
            if resp.status_code == 409:
                # Sold out / purchase limit is an expected business answer,
                # not a failure - keep the failure chart meaningful.
                resp.success()
