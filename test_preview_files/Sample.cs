using System;

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
