CREATE TABLE airport
(
    id      SERIAL PRIMARY KEY,
    name    VARCHAR(255),
    city    VARCHAR(255),
    country VARCHAR(255)
);


CREATE TABLE flight
(
    id              SERIAL PRIMARY KEY,
    flight_number   VARCHAR(20)              NOT NULL,
    datetime        TIMESTAMP WITH TIME ZONE NOT NULL,
    from_airport_id INT REFERENCES airport (id),
    to_airport_id   INT REFERENCES airport (id),
    price           INT                      NOT NULL,
    capacity        INT                      NOT NULL DEFAULT 100,
    bought          INT                      NOT NULL DEFAULT 0
        CHECK (bought >= 0)
);


INSERT INTO airport (name, city, country) VALUES
('Пулково', 'Санкт-Петербург', 'Россия'),
('Шереметьево', 'Москва', 'Россия'),
('Внуково', 'Москва', 'Россия'),
('Домодедово', 'Москва', 'Россия'),
('Кольцово', 'Екатеринбург', 'Россия'),
('Толмачево', 'Новосибирск', 'Россия'),
('Казань', 'Казань', 'Россия'),
('Минеральные Воды', 'Минеральные Воды', 'Россия'),
('Сочи', 'Сочи', 'Россия'),
('Краснодар', 'Краснодар', 'Россия');

INSERT INTO flight (flight_number, datetime, from_airport_id, to_airport_id, price, capacity, bought)
VALUES ('AFL031', '2021-10-08 20:00:00+03', 
        (SELECT id FROM airport WHERE name='Пулково'),
        (SELECT id FROM airport WHERE name='Шереметьево'),
        1500, 100, 0);

INSERT INTO flight (flight_number, datetime, from_airport_id, to_airport_id, price, capacity, bought) VALUES
('AFL032', '2021-10-09 10:30:00+03', 1, 2, 1200, 100, 0),
('AFL033', '2021-10-09 15:45:00+03', 2, 3, 1800, 100, 0),
('AFL034', '2021-10-10 08:00:00+03', 3, 4, 2000, 100, 0),
('AFL035', '2021-10-10 12:15:00+03', 4, 5, 1600, 100, 0),
('AFL036', '2021-10-11 09:00:00+03', 5, 6, 2200, 100, 0),
('AFL037', '2021-10-11 17:30:00+03', 6, 7, 1400, 100, 0),
('AFL038', '2021-10-12 06:45:00+03', 7, 8, 1300, 100, 0),
('AFL039', '2021-10-12 14:00:00+03', 8, 9, 1700, 100, 0),
('AFL040', '2021-10-12 19:20:00+03', 9, 10, 1500, 100, 0);
