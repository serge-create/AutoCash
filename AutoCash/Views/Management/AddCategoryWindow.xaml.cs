using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AutoCash.Models;

namespace AutoCash.Views.Management
{
    public partial class AddCategoryWindow : Window
    {
        public AddCategoryWindow()
        {
            InitializeComponent();
            txtCategoryName.Focus(); // Автофокус на ввод
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            string categoryName = txtCategoryName.Text.Trim();

            if (string.IsNullOrEmpty(categoryName))
            {
                MessageBox.Show("Введите наименование товарной группы!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var db = new AutoCashierDbEntities1())
                {
                    // Проверка на дубликаты без учета регистра букв
                    bool isDuplicate = db.Product_Groups.Any(g => g.Name.ToLower() == categoryName.ToLower());
                    if (isDuplicate)
                    {
                        MessageBox.Show("Такая товарная группа уже существует в справочнике!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Создаем новую запись справочника
                    var newGroup = new Product_Groups
                    {
                        Name = categoryName
                    };

                    db.Product_Groups.Add(newGroup);
                    db.SaveChanges();
                }

                this.DialogResult = true; // Сигнализируем об успешном сохранении
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при работе с базой данных:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) btnSave_Click(null, null);
            else if (e.Key == Key.Escape) btnCancel_Click(null, null);
        }
    }
}