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
