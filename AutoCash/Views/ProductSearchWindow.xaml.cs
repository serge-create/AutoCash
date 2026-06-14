using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AutoCash.Models; // Твои модели БД

namespace AutoCash.Views
{
    public partial class ProductSearchWindow : Window
    {
        // Кэш всех товаров из базы для быстрого поиска без лишних запросов к СУБД
        private List<Products> _allProducts;

        // Свойство, в которое мы положим товар, чтобы главное окно могло его забрать
        public Products SelectedProduct { get; private set; }

        public ProductSearchWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadProducts();
            txtSearch.Focus(); // Сразу ставим курсор в поле поиска
        }

        private void LoadProducts()
        {
            try
            {
                using (var db = new Models.AutoCashierDbEntities1())
                {
                    // Загружаем товары вместе с налоговой ставкой (нужно для правильного чека)
                    _allProducts = db.Products
                        .Include(p => p.Tax_Rates)
                        .Where(p => p.StockQuantity > 0)
                        .ToList();
                }
                dgProducts.ItemsSource = _allProducts;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки базы товаров: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Динамический поиск "на лету"
        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allProducts == null) return;

            string query = txtSearch.Text.ToLower().Trim();

            if (string.IsNullOrEmpty(query))
            {
                dgProducts.ItemsSource = _allProducts;
            }
            else
            {
                // Ищем совпадения по имени или штрихкоду
                var filtered = _allProducts.Where(p =>
                    p.Name.ToLower().Contains(query) ||
                    (p.Barcode != null && p.Barcode.Contains(query))
                ).ToList();

                dgProducts.ItemsSource = filtered;
            }
        }

        // Выбор товара по двойному клику
        private void dgProducts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ConfirmSelection();
        }

        private void dgProducts_MouseDown(object sender, MouseButtonEventArgs e)
        {
            AutoCash.Core.WpfUtils.HandleDataGridMouseDown(sender, e);
        }

        // Выбор товара по кнопке
        private void btnSelect_Click(object sender, RoutedEventArgs e)
        {
            ConfirmSelection();
        }

        private void ConfirmSelection()
        {
            if (dgProducts.SelectedItem is Products product)
            {
                SelectedProduct = product;
                this.DialogResult = true; // Возвращаем true в главное окно
                this.Close();
            }
            else
            {
                MessageBox.Show("Пожалуйста, выделите товар в таблице.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        // Удобство работы с клавиатурой
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                btnCancel_Click(null, null);
            }
            else if (e.Key == Key.Enter)
            {
                ConfirmSelection();
            }
            else if (e.Key == Key.Down && txtSearch.IsFocused)
            {
                // Если кассир нажал "Вниз" находясь в строке поиска - переводим фокус на таблицу
                if (dgProducts.Items.Count > 0)
                {
                    dgProducts.Focus();
                    dgProducts.SelectedIndex = 0;
                }
            }
            else if (e.Key == Key.F3)
            {
                txtSearch.Focus();
                txtSearch.SelectAll();
            }
        }
    }
}