#!/usr/bin/env python3
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
