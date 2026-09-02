CREATE TABLE privilege
(
    id       SERIAL PRIMARY KEY,
    username VARCHAR(80) NOT NULL UNIQUE,
    status   VARCHAR(80) NOT NULL DEFAULT 'BRONZE'
        CHECK (status IN ('BRONZE', 'SILVER', 'GOLD')),
    balance  INT NOT NULL DEFAULT 0
);

CREATE TABLE privilege_history
(
    id             SERIAL PRIMARY KEY,
    privilege_id   INT REFERENCES privilege (id),
    ticket_uid     uuid        NOT NULL,
    datetime       TIMESTAMP   NOT NULL,
    balance_diff   INT         NOT NULL,
    operation_type VARCHAR(20) NOT NULL
        CHECK (operation_type IN ('FILL_IN_BALANCE', 'DEBIT_THE_ACCOUNT'))
);

INSERT INTO privilege (username, status, balance)
VALUES
('Test Max', 'GOLD', 1500),
('Alice', 'BRONZE', 0),
('Bob', 'SILVER', 5200),
('Charlie', 'GOLD', 10250),
('David', 'BRONZE', 300);

INSERT INTO privilege_history (privilege_id, ticket_uid, datetime, balance_diff, operation_type)
VALUES
((SELECT id FROM privilege WHERE username='Test Max'), '049161bb-badd-4fa8-9d90-87c9a82b0668', '2021-10-08T19:59:19Z', 1500, 'FILL_IN_BALANCE'),
((SELECT id FROM privilege WHERE username='Alice'), gen_random_uuid(), now() - interval '3 days', 200, 'FILL_IN_BALANCE'),
((SELECT id FROM privilege WHERE username='Alice'), gen_random_uuid(), now() - interval '2 days', -50, 'DEBIT_THE_ACCOUNT'),
((SELECT id FROM privilege WHERE username='Bob'), gen_random_uuid(), now() - interval '5 days', 500, 'FILL_IN_BALANCE'),
((SELECT id FROM privilege WHERE username='Charlie'), gen_random_uuid(), now() - interval '1 day', -1000, 'DEBIT_THE_ACCOUNT'),
((SELECT id FROM privilege WHERE username='David'), gen_random_uuid(), now() - interval '7 days', 300, 'FILL_IN_BALANCE');
