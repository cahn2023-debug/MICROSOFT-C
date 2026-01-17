#!/usr/bin/env python3
"""
Preview Test Script for OfflineProjectManager

This script creates sample test files for testing the preview functionality.
Run this script to generate test files, then open them in OfflineProjectManager.
"""

import os
import sys

def create_test_directory():
    """Create a test directory with sample files for preview testing."""
    
    test_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "test_preview_files")
    os.makedirs(test_dir, exist_ok=True)
    
    print(f"Creating test files in: {test_dir}")
    print("-" * 50)
    
    # 1. Text file
    txt_file = os.path.join(test_dir, "sample.txt")
    with open(txt_file, "w", encoding="utf-8") as f:
        f.write("This is a sample text file for preview testing.\n")
        f.write("It contains multiple lines of text.\n")
        f.write("Line 3: Testing preview functionality.\n")
        f.write("Line 4: Hello World!\n")
    print(f"✅ Created: {txt_file}")
    
    # 2. JSON file
    json_file = os.path.join(test_dir, "sample.json")
    with open(json_file, "w", encoding="utf-8") as f:
        f.write('''{
    "name": "Preview Test",
    "version": "1.0.0",
    "description": "Sample JSON file for preview testing",
    "features": [
        "Syntax highlighting",
        "Dark/Light theme support",
        "Line numbers"
    ],
    "settings": {
        "enabled": true,
        "maxSize": 5000000
    }
}''')
    print(f"✅ Created: {json_file}")
    
    # 3. Python file
    py_file = os.path.join(test_dir, "sample.py")
    with open(py_file, "w", encoding="utf-8") as f:
        f.write('''#!/usr/bin/env python3
"""Sample Python file for preview testing."""

def hello_world():
    """Print hello world message."""
    print("Hello, World!")
    return True

class Calculator:
    """A simple calculator class."""
    
    def __init__(self):
        self.result = 0
    
    def add(self, a, b):
        """Add two numbers."""
        self.result = a + b
        return self.result
    
    def subtract(self, a, b):
        """Subtract two numbers."""
        self.result = a - b
        return self.result

if __name__ == "__main__":
    hello_world()
    calc = Calculator()
    print(f"5 + 3 = {calc.add(5, 3)}")
''')
    print(f"✅ Created: {py_file}")
    
    # 4. C# file
    cs_file = os.path.join(test_dir, "Sample.cs")
    with open(cs_file, "w", encoding="utf-8") as f:
        f.write('''using System;

namespace PreviewTest
{
    /// <summary>
    /// Sample C# class for preview testing.
    /// </summary>
    public class Sample
    {
        public string Name { get; set; }
        public int Value { get; set; }
        
        public Sample(string name, int value)
        {
            Name = name;
            Value = value;
        }
        
        public void PrintInfo()
        {
            Console.WriteLine($"Name: {Name}, Value: {Value}");
        }
    }
    
    class Program
    {
        static void Main(string[] args)
        {
            var sample = new Sample("Test", 42);
            sample.PrintInfo();
        }
    }
}
''')
    print(f"✅ Created: {cs_file}")
    
    # 5. HTML file
    html_file = os.path.join(test_dir, "sample.html")
    with open(html_file, "w", encoding="utf-8") as f:
        f.write('''<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Preview Test</title>
    <style>
        body {
            font-family: 'Segoe UI', sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 40px;
        }
        h1 { color: #fff; }
        .card {
            background: white;
            color: #333;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.2);
        }
    </style>
</head>
<body>
    <h1>Preview Test Page</h1>
    <div class="card">
        <h2>Welcome</h2>
        <p>This is a sample HTML file for testing the preview functionality.</p>
    </div>
</body>
</html>
''')
    print(f"✅ Created: {html_file}")
    
    # 6. CSS file
    css_file = os.path.join(test_dir, "sample.css")
    with open(css_file, "w", encoding="utf-8") as f:
        f.write('''/* Sample CSS file for preview testing */

:root {
    --primary-color: #007ACC;
    --secondary-color: #3E3E42;
    --text-color: #CCCCCC;
    --background-color: #1E1E1E;
}

body {
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    background-color: var(--background-color);
    color: var(--text-color);
    margin: 0;
    padding: 20px;
    line-height: 1.6;
}

.container {
    max-width: 1200px;
    margin: 0 auto;
    padding: 20px;
}

.button {
    background-color: var(--primary-color);
    color: white;
    border: none;
    padding: 10px 20px;
    border-radius: 4px;
    cursor: pointer;
    transition: background-color 0.3s ease;
}

.button:hover {
    background-color: #005a9e;
}

/* Responsive design */
@media (max-width: 768px) {
    .container {
        padding: 10px;
    }
}
''')
    print(f"✅ Created: {css_file}")
    
    # 7. Markdown file
    md_file = os.path.join(test_dir, "README.md")
    with open(md_file, "w", encoding="utf-8") as f:
        f.write('''# Preview Test

This is a sample **Markdown** file for testing.

## Features

- Syntax highlighting
- Dark/Light theme support
- Line numbers

## Code Example

```python
def hello():
    print("Hello, World!")
```

## Table

| Feature | Status |
|---------|--------|
| Images | ✅ |
| Text | ✅ |
| PDF | ✅ |

---

*Created for preview testing*
''')
    print(f"✅ Created: {md_file}")
    
    # 8. XML file
    xml_file = os.path.join(test_dir, "sample.xml")
    with open(xml_file, "w", encoding="utf-8") as f:
        f.write('''<?xml version="1.0" encoding="UTF-8"?>
<project>
    <name>Preview Test</name>
    <version>1.0.0</version>
    <description>Sample XML file for preview testing</description>
    <features>
        <feature name="Syntax highlighting" enabled="true"/>
        <feature name="Dark theme" enabled="true"/>
        <feature name="Line numbers" enabled="true"/>
    </features>
    <settings>
        <setting key="maxSize" value="5000000"/>
        <setting key="timeout" value="10"/>
    </settings>
</project>
''')
    print(f"✅ Created: {xml_file}")
    
    # 9. CSV file
    csv_file = os.path.join(test_dir, "sample.csv")
    with open(csv_file, "w", encoding="utf-8") as f:
        f.write('''id,name,value,status
1,Item A,100,active
2,Item B,200,inactive
3,Item C,300,active
4,Item D,400,pending
5,Item E,500,active
''')
    print(f"✅ Created: {csv_file}")
    
    # 10. SQL file
    sql_file = os.path.join(test_dir, "sample.sql")
    with open(sql_file, "w", encoding="utf-8") as f:
        f.write('''-- Sample SQL file for preview testing
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
''')
    print(f"✅ Created: {sql_file}")
    
    print("-" * 50)
    print(f"\n🎉 Test files created successfully!")
    print(f"\nTo test preview functionality:")
    print(f"1. Open OfflineProjectManager")
    print(f"2. Create or open a project")
    print(f"3. Add folder: {test_dir}")
    print(f"4. Click on each file to test preview")
    
    return test_dir

if __name__ == "__main__":
    create_test_directory()
