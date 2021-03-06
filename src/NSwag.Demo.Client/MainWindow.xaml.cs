﻿using System;
using System.Windows;

namespace NSwag.Demo.Client
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void OnCalculate(object sender, RoutedEventArgs e)
        {
            try
            {
                var client = new PersonsClient("http://localhost:22093");
                var result = await client.CalculateAsync(int.Parse(A.Text), int.Parse(B.Text), int.Parse(C.Text));
                Result.Text = result.ToString();

                var persons = await client.GetAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
    }
}
