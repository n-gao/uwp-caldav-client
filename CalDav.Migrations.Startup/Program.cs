using System;
using CalDav.Models;

namespace CalDav.Migrations.Startup
{
    class Program
    {
        static void Main(string[] args)
        {
            var db = new CalDavContext();
            Console.WriteLine("Hello World!");
        }
    }
}
