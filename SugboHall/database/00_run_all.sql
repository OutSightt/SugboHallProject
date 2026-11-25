-- SugboHall: schema + procedures + seed data in one script (MySQL Workbench > Execute)

-- ============================================================
-- SugboHall — Cebu Community Events & Venue Booking (MySQL)
-- ============================================================

CREATE DATABASE IF NOT EXISTS sugbo_events;
USE sugbo_events;

DROP TABLE IF EXISTS booking_audit;
DROP TABLE IF EXISTS hall_bookings;
DROP TABLE IF EXISTS halls;
DROP TABLE IF EXISTS residents;

-- 1. RESIDENTS
CREATE TABLE residents (
    resident_id   INT AUTO_INCREMENT PRIMARY KEY,
    first_name    VARCHAR(50)  NOT NULL,
    last_name     VARCHAR(50)  NOT NULL,
    email         VARCHAR(100) NOT NULL UNIQUE,
    phone         VARCHAR(20)  NOT NULL,
    address       VARCHAR(200) DEFAULT NULL,
    created_at    TIMESTAMP DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

-- 2. HALLS
CREATE TABLE halls (
    hall_id       INT AUTO_INCREMENT PRIMARY KEY,
    hall_name     VARCHAR(100) NOT NULL UNIQUE,
    location      VARCHAR(200) NOT NULL,
    capacity      INT          NOT NULL,
    rate_per_hour DECIMAL(10,2) NOT NULL,
    status        ENUM('Available','Under Maintenance','Closed') DEFAULT 'Available',
    description   TEXT,
    created_at    TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT chk_capacity      CHECK (capacity > 0),
    CONSTRAINT chk_rate_per_hour CHECK (rate_per_hour > 0)
) ENGINE=InnoDB;

-- 3. HALL_BOOKINGS
CREATE TABLE hall_bookings (
    booking_id    INT AUTO_INCREMENT PRIMARY KEY,
    resident_id   INT          NOT NULL,
    hall_id       INT          NOT NULL,
    event_name    VARCHAR(100) NOT NULL,
    event_date    DATE         NOT NULL,
    start_time    TIME         NOT NULL,
    end_time      TIME         NOT NULL,
    total_charge  DECIMAL(10,2) DEFAULT 0.00,
    status        ENUM('Pending','Confirmed','Cancelled','Completed') DEFAULT 'Pending',
    created_at    TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at    TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT fk_booking_resident FOREIGN KEY (resident_id) REFERENCES residents(resident_id) ON DELETE RESTRICT,
    CONSTRAINT fk_booking_hall     FOREIGN KEY (hall_id)     REFERENCES halls(hall_id)         ON DELETE RESTRICT,
    CONSTRAINT chk_time_range      CHECK (end_time > start_time)
) ENGINE=InnoDB;

CREATE INDEX idx_booking_date   ON hall_bookings (event_date);
CREATE INDEX idx_booking_status ON hall_bookings (status);

-- 4. BOOKING_AUDIT
CREATE TABLE booking_audit (
    audit_id        INT AUTO_INCREMENT PRIMARY KEY,
    booking_id      INT         NOT NULL,
    action          VARCHAR(50) NOT NULL,
    old_status      VARCHAR(20),
    new_status      VARCHAR(20),
    details         TEXT,
    audit_timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_audit_booking FOREIGN KEY (booking_id) REFERENCES hall_bookings(booking_id) ON DELETE CASCADE
) ENGINE=InnoDB;

USE sugbo_events;

-- ══════════════════════════════════════════════
-- STORED FUNCTION: CalculateRentalCharge
-- ══════════════════════════════════════════════
DROP FUNCTION IF EXISTS CalculateRentalCharge;
DELIMITER //
CREATE FUNCTION CalculateRentalCharge(
    p_hall_id    INT,
    p_start_time TIME,
    p_end_time   TIME
) RETURNS DECIMAL(10,2)
DETERMINISTIC READS SQL DATA
BEGIN
    DECLARE v_rate   DECIMAL(10,2);
    DECLARE v_hours  DECIMAL(5,2);
    SELECT rate_per_hour INTO v_rate FROM halls WHERE hall_id = p_hall_id;
    IF v_rate IS NULL THEN RETURN 0.00; END IF;
    SET v_hours = TIMESTAMPDIFF(MINUTE, p_start_time, p_end_time) / 60.0;
    RETURN ROUND(v_rate * v_hours, 2);
END //
DELIMITER ;

-- ══════════════════════════════════════════════
-- STORED PROCEDURE: SubmitReservation
-- ══════════════════════════════════════════════
DROP PROCEDURE IF EXISTS SubmitReservation;
DELIMITER //
CREATE PROCEDURE SubmitReservation(
    IN  p_resident_id INT,
    IN  p_hall_id     INT,
    IN  p_event_name  VARCHAR(100),
    IN  p_event_date  DATE,
    IN  p_start_time  TIME,
    IN  p_end_time    TIME,
    OUT p_booking_id  INT,
    OUT p_message     VARCHAR(255)
)
BEGIN
    DECLARE v_hall_status  VARCHAR(20);
    DECLARE v_conflict_cnt INT DEFAULT 0;
    DECLARE v_charge       DECIMAL(10,2);
    DECLARE v_resident_cnt INT DEFAULT 0;

    SELECT COUNT(*) INTO v_resident_cnt FROM residents WHERE resident_id = p_resident_id;
    IF v_resident_cnt = 0 THEN
        SET p_booking_id = -1; SET p_message = 'Error: Resident not found.';
    ELSE
        SELECT status INTO v_hall_status FROM halls WHERE hall_id = p_hall_id;
        IF v_hall_status IS NULL THEN
            SET p_booking_id = -1; SET p_message = 'Error: Hall not found.';
        ELSEIF v_hall_status != 'Available' THEN
            SET p_booking_id = -1; SET p_message = CONCAT('Error: Hall is currently "', v_hall_status, '".');
        ELSE
            SELECT COUNT(*) INTO v_conflict_cnt FROM hall_bookings
            WHERE hall_id = p_hall_id AND event_date = p_event_date
              AND status IN ('Pending','Confirmed')
              AND ((p_start_time >= start_time AND p_start_time < end_time)
                OR (p_end_time > start_time AND p_end_time <= end_time)
                OR (p_start_time <= start_time AND p_end_time >= end_time));

            IF v_conflict_cnt > 0 THEN
                SET p_booking_id = -1; SET p_message = 'Error: Time slot conflicts with an existing booking.';
            ELSE
                SET v_charge = CalculateRentalCharge(p_hall_id, p_start_time, p_end_time);
                INSERT INTO hall_bookings (resident_id, hall_id, event_name, event_date, start_time, end_time, total_charge, status)
                VALUES (p_resident_id, p_hall_id, p_event_name, p_event_date, p_start_time, p_end_time, v_charge, 'Pending');
                SET p_booking_id = LAST_INSERT_ID();
                SET p_message = CONCAT('Reservation submitted! Total charge: P', FORMAT(v_charge, 2));
            END IF;
        END IF;
    END IF;
END //
DELIMITER ;

-- ══════════════════════════════════════════════
-- TRIGGER: trg_booking_status_audit
-- ══════════════════════════════════════════════
DROP TRIGGER IF EXISTS trg_booking_status_audit;
DELIMITER //
CREATE TRIGGER trg_booking_status_audit
AFTER UPDATE ON hall_bookings
FOR EACH ROW
BEGIN
    IF OLD.status != NEW.status THEN
        INSERT INTO booking_audit (booking_id, action, old_status, new_status, details)
        VALUES (
            NEW.booking_id,
            CASE NEW.status WHEN 'Cancelled' THEN 'CANCELLATION' WHEN 'Confirmed' THEN 'CONFIRMATION' WHEN 'Completed' THEN 'COMPLETION' ELSE 'STATUS_CHANGE' END,
            OLD.status, NEW.status,
            CONCAT('Booking #', NEW.booking_id, ' for "', NEW.event_name, '" changed from ', OLD.status, ' to ', NEW.status, '.')
        );
    END IF;
END //
DELIMITER ;

USE sugbo_events;

INSERT INTO residents (first_name, last_name, email, phone, address) VALUES
('Jose Mari', 'Abellana',  'josemari.abellana@email.com', '09171234567', 'Sitio Maguikay, Brgy. Lahug, Cebu City'),
('Maricel',   'Cabahug',   'maricel.cabahug@email.com',   '09221234568', 'Brgy. Guadalupe, Cebu City'),
('Rodrigo',   'Tumulak',   'rodrigo.tumulak@email.com',   '09321234569', 'Brgy. Subangdaku, Mandaue City'),
('Jonalyn',   'Gaviola',   'jonalyn.gaviola@email.com',   '09181234570', 'Brgy. Pajo, Lapu-Lapu City'),
('Nenita',    'Pepito',    'nenita.pepito@email.com',     '09951234571', 'Brgy. Mabolo, Cebu City'),
('Junjun',    'Villaflor', 'junjun.villaflor@email.com',  '09061234572', 'Brgy. Bulacao, Talisay City');

INSERT INTO halls (hall_name, location, capacity, rate_per_hour, status, description) VALUES
('Lahug Barangay Hall',            'Brgy. Lahug, Cebu City',        120, 400.00, 'Available',         'Barangay hall with stage and sound system. Good for assemblies, dance practice and Christmas parties.'),
('Guadalupe Multi-Purpose Hall',   'Brgy. Guadalupe, Cebu City',     80, 350.00, 'Available',         'Covered multi-purpose hall with electric fans and monobloc chairs.'),
('Mabolo Community Function Room', 'Brgy. Mabolo, Cebu City',        40, 250.00, 'Available',         'Small air-conditioned room for fellowships, meetings and trainings.'),
('Sugbo Open-Air Pavilion',        'Mandaue City Seaside Park',     250, 900.00, 'Available',         'Open-air pavilion for fiestas, reunions and big salo-salo. Lechon-friendly cooking area nearby.'),
('Banilad Heritage Hall',          'Brgy. Banilad, Cebu City',      100, 500.00, 'Under Maintenance', 'Old hall currently being repainted and repaired.');

INSERT INTO hall_bookings (resident_id, hall_id, event_name, event_date, start_time, end_time, total_charge, status) VALUES
(1, 1, 'Sinulog Dance Contingent Practice',  '2026-12-05', '15:00:00', '19:00:00', 1600.00, 'Confirmed'),
(2, 2, 'Barangay Christmas Party',           '2026-12-12', '17:00:00', '21:00:00', 1400.00, 'Pending'),
(3, 3, 'Simbang Gabi Fellowship',            '2026-12-20', '18:00:00', '20:00:00',  500.00, 'Confirmed'),
(4, 4, 'Fiesta Sto. Nino Salo-salo',         '2027-01-16', '10:00:00', '18:00:00', 7200.00, 'Pending'),
(1, 2, 'Barangay Assembly',                  '2026-11-14', '13:00:00', '15:00:00',  700.00, 'Completed'),
(5, 1, 'Zumba sa Barangay',                  '2026-11-29', '06:00:00', '08:00:00',  800.00, 'Cancelled');

INSERT INTO booking_audit (booking_id, action, old_status, new_status, details) VALUES
(1, 'CONFIRMATION', 'Pending',   'Confirmed', 'Booking #1 for "Sinulog Dance Contingent Practice" changed from Pending to Confirmed.'),
(3, 'CONFIRMATION', 'Pending',   'Confirmed', 'Booking #3 for "Simbang Gabi Fellowship" changed from Pending to Confirmed.'),
(5, 'CONFIRMATION', 'Pending',   'Confirmed', 'Booking #5 for "Barangay Assembly" changed from Pending to Confirmed.'),
(5, 'COMPLETION',   'Confirmed', 'Completed', 'Booking #5 for "Barangay Assembly" changed from Confirmed to Completed.'),
(6, 'CANCELLATION', 'Pending',   'Cancelled', 'Booking #6 for "Zumba sa Barangay" changed from Pending to Cancelled.');
