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
