CREATE TABLE ticket
(
    id            SERIAL PRIMARY KEY,
    ticket_uid    uuid UNIQUE NOT NULL,
    username      VARCHAR(80) NOT NULL,
    flight_number VARCHAR(20) NOT NULL,
    price         INT         NOT NULL,
    status        VARCHAR(20) NOT NULL
        CHECK (status IN ('PAID', 'CANCELED'))
);

INSERT INTO ticket (ticket_uid, username, flight_number, price, status)
VALUES (
    '049161bb-badd-4fa8-9d90-87c9a82b0668',
    'ivan',
    'AFL031',
    1500,
    'PAID'
);
INSERT INTO ticket (ticket_uid, username, flight_number, price, status)
VALUES
(gen_random_uuid(), 'Test Max', 'AFL031', 1500, 'PAID'),
(gen_random_uuid(), 'Alice', 'AFL032', 1200, 'PAID'),
(gen_random_uuid(), 'Bob', 'AFL033', 1800, 'CANCELED'),
(gen_random_uuid(), 'Charlie', 'AFL034', 2000, 'PAID'),
(gen_random_uuid(), 'David', 'AFL035', 1600, 'PAID'),
(gen_random_uuid(), 'Alice', 'AFL036', 2200, 'CANCELED'),
(gen_random_uuid(), 'Bob', 'AFL037', 1400, 'PAID'),
(gen_random_uuid(), 'Charlie', 'AFL038', 1300, 'PAID'),
(gen_random_uuid(), 'David', 'AFL039', 1700, 'CANCELED'),
(gen_random_uuid(), 'Test Max', 'AFL040', 1500, 'PAID');