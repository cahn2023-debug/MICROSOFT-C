-- Sample SQL file for preview testing
-- Create a sample table

CREATE TABLE users (
    id INT PRIMARY KEY AUTO_INCREMENT,
    username VARCHAR(50) NOT NULL,
    email VARCHAR(100) UNIQUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Insert sample data
INSERT INTO users (username, email) VALUES
('john_doe', 'john@example.com'),
('jane_smith', 'jane@example.com'),
('bob_wilson', 'bob@example.com');

-- Select all users
SELECT * FROM users WHERE id > 0 ORDER BY created_at DESC;

-- Update a user
UPDATE users SET email = 'new_email@example.com' WHERE username = 'john_doe';
