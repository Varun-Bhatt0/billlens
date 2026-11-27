-- =============================================================================
-- BillLens - Oracle schema (Oracle 12c or later)
--
-- Run as the application schema owner, e.g.:
--   sqlplus bills_owner@//localhost:1521/FREEPDB1 @db/oracle/schema.sql
--
-- Least privilege: the web app should connect as a separate user that only has
-- the grants at the end of this file, not as the schema owner.
--
-- Matches the SQLite schema in Services/SqliteMigrations.cs.
-- =============================================================================

-- ---------------------------------------------------------------- tables

CREATE TABLE users (
    employee_id       VARCHAR2(20)  PRIMARY KEY,
    emp_name       VARCHAR2(100) NOT NULL,
    password_hash  VARCHAR2(100) NOT NULL,                 -- BCrypt hash, cost 12
    is_admin       CHAR(1)       DEFAULT 'N' NOT NULL CHECK (is_admin IN ('Y', 'N')),
    created_date   DATE          DEFAULT SYSDATE NOT NULL,
    last_login     DATE
);

CREATE TABLE bill_type_master (
    bill_type_no   NUMBER(5)     PRIMARY KEY,
    bill_type      VARCHAR2(100) NOT NULL UNIQUE,
    description    VARCHAR2(200),
    is_active      CHAR(1)       DEFAULT 'Y' NOT NULL CHECK (is_active IN ('Y', 'N')),
    created_date   DATE          DEFAULT SYSDATE NOT NULL
);

CREATE TABLE bill_applications (
    application_sno   NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    employee_id          VARCHAR2(20) NOT NULL REFERENCES users (employee_id),
    bill_type_no      NUMBER(5)    NOT NULL REFERENCES bill_type_master (bill_type_no),
    application_date  DATE         DEFAULT SYSDATE NOT NULL,
    status            VARCHAR2(20) DEFAULT 'SUBMITTED' NOT NULL
                      CHECK (status IN ('SUBMITTED', 'APPROVED', 'REJECTED'))
);

CREATE TABLE bill_details (
    detail_sno       NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    application_sno  NUMBER        NOT NULL REFERENCES bill_applications (application_sno) ON DELETE CASCADE,
    bill_no          VARCHAR2(50)  NOT NULL,
    bill_date        DATE          NOT NULL,
    bill_amount      NUMBER(15, 2) NOT NULL CHECK (bill_amount >= 0),
    image_path       VARCHAR2(500),
    created_date     DATE          DEFAULT SYSDATE NOT NULL
);

CREATE TABLE audit_log (
    log_id    NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    employee_id  VARCHAR2(20),
    action    VARCHAR2(100) NOT NULL,
    details   VARCHAR2(4000),
    log_time  TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL
);

-- ---------------------------------------------------------------- indexes

CREATE INDEX ix_bill_applications_user_date ON bill_applications (employee_id, application_date DESC);
CREATE INDEX ix_bill_details_application    ON bill_details (application_sno);
CREATE INDEX ix_audit_log_user              ON audit_log (employee_id);

-- ---------------------------------------------------------------- procedure
-- Called by DatabaseManager.SaveBillOracle with parameters in this order (bound by position).
-- p_bill_date arrives as DD/MM/YYYY; p_bill_amt as a plain decimal string.

CREATE OR REPLACE PROCEDURE save_bill_secure (
    p_employee_id            IN  VARCHAR2,
    p_type           IN  NUMBER,
    p_bill_no        IN  VARCHAR2,
    p_bill_date      IN  VARCHAR2,
    p_bill_amt       IN  VARCHAR2,
    p_image_path     IN  VARCHAR2,
    p_ip             IN  VARCHAR2,
    p_agent          IN  VARCHAR2,
    out_app_sno      OUT NUMBER,
    out_detail_sno   OUT NUMBER
) AS
BEGIN
    INSERT INTO bill_applications (employee_id, bill_type_no)
    VALUES (p_employee_id, p_type)
    RETURNING application_sno INTO out_app_sno;

    INSERT INTO bill_details (application_sno, bill_no, bill_date, bill_amount, image_path)
    VALUES (out_app_sno,
            p_bill_no,
            TO_DATE(p_bill_date, 'DD/MM/YYYY'),
            TO_NUMBER(p_bill_amt, '9999999999999.99', 'NLS_NUMERIC_CHARACTERS=''.,'''),
            NULLIF(p_image_path, ''))
    RETURNING detail_sno INTO out_detail_sno;

    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE;
END save_bill_secure;
/

-- ---------------------------------------------------------------- reference data

INSERT INTO bill_type_master (bill_type_no, bill_type, description) VALUES (1, 'Medical Bills',   'Medical reimbursement');
INSERT INTO bill_type_master (bill_type_no, bill_type, description) VALUES (2, 'Travel Expenses', 'Official travel');
INSERT INTO bill_type_master (bill_type_no, bill_type, description) VALUES (3, 'Office Supplies', 'Stationery and supplies');
INSERT INTO bill_type_master (bill_type_no, bill_type, description) VALUES (4, 'Utility Bills',   'Electricity, water, phone');
COMMIT;

-- ---------------------------------------------------------------- application user (least privilege)
-- Run as a DBA, replacing bills_owner / bills_app and choosing a strong password:
--
--   CREATE USER bills_app IDENTIFIED BY "<strong password>";
--   GRANT CREATE SESSION TO bills_app;
--   GRANT SELECT, UPDATE (last_login)  ON bills_owner.users             TO bills_app;
--   GRANT SELECT                       ON bills_owner.bill_type_master  TO bills_app;
--   GRANT SELECT                       ON bills_owner.bill_applications TO bills_app;
--   GRANT SELECT                       ON bills_owner.bill_details      TO bills_app;
--   GRANT SELECT, INSERT               ON bills_owner.audit_log         TO bills_app;
--   GRANT EXECUTE                      ON bills_owner.save_bill_secure  TO bills_app;
--   -- then create synonyms for bills_app, or set ALTER SESSION SET CURRENT_SCHEMA = bills_owner
--
-- Users are created by an administrator with a BCrypt hash (cost 12), e.g.:
--   INSERT INTO users (employee_id, emp_name, password_hash, is_admin) VALUES ('100234', 'A. Kumar', '$2a$12$...', 'N');
