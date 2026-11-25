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
