using System;
using System.Windows.Forms;

namespace BatIDE
{
    internal static class Program
    {
        /// <summary>
        /// Головна точка входу застосунку.
        /// У .NET 8 WinForms ініціалізацію візуальних стилів
        /// та DPI-режиму виконує згенерований метод
        /// ApplicationConfiguration.Initialize().
        /// </summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }
}
