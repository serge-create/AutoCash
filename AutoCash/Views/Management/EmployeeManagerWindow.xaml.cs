using System;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AutoCash.Core;
using AutoCash.Models; // Твои классы Entity Framework

namespace AutoCash.Views.Management
{
    public partial class EmployeeManagerWindow : Window
    {
        private Employees _selectedEmployee = null;

        public EmployeeManagerWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Дополнительная проверка безопасности: вдруг окно попытались открыть не из под Админа
            if (AppState.CurrentUser.Roles?.RoleName != "Администратор")
            {
                MessageBox.Show("У вас нет прав для доступа к этому разделу.", "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
                return;
            }

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                using (var db = new AutoCashierDbEntities1()) // Замени на свой Context
                {
                    // Загружаем список должностей для выпадающего списка
                    cbRoles.ItemsSource = db.Roles.ToList();

                    // Загружаем сотрудников с их ролями
                    dgEmployees.ItemsSource = db.Employees.Include(emp => emp.Roles).ToList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Выбор сотрудника в таблице -> заполнение карточки справа
        private void dgEmployees_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgEmployees.SelectedItem is Employees emp)
            {
                _selectedEmployee = emp;
                txtFullName.Text = emp.FullName;
                txtPinCode.Text = emp.PinCode;
                cbRoles.SelectedValue = emp.RoleID;

                lblCardTitle.Text = "Редактирование профиля";
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            _selectedEmployee = null;
            txtFullName.Clear();
            txtPinCode.Clear();
            cbRoles.SelectedIndex = -1;
            lblCardTitle.Text = "Новый сотрудник";
            dgEmployees.SelectedItem = null;
        }

        // Сохранение (или добавление) сотрудника
        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFullName.Text) || string.IsNullOrWhiteSpace(txtPinCode.Text) || cbRoles.SelectedItem == null)
            {
                MessageBox.Show("Заполните все поля (ФИО, Роль, ПИН-код)!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string newPin = txtPinCode.Text.Trim();

            try
            {
                using (var db = new AutoCashierDbEntities1())
                {
                    // ПРОВЕРКА НА УНИКАЛЬНОСТЬ ПИН-КОДА
                    // Ищем, есть ли в базе кто-то с таким же ПИНом, кроме самого редактируемого сотрудника
                    int currentEmpId = _selectedEmployee?.EmployeeID ?? 0;
                    bool pinExists = db.Employees.Any(emp => emp.PinCode == newPin && emp.EmployeeID != currentEmpId);

                    if (pinExists)
                    {
                        MessageBox.Show("Сотрудник с таким ПИН-кодом уже существует! Введите другой ПИН-код во избежание конфликтов авторизации.",
                                        "Ошибка уникальности", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (_selectedEmployee == null)
                    {
                        // Добавление нового сотрудника
                        var newEmp = new Employees
                        {
                            FullName = txtFullName.Text.Trim(),
                            PinCode = newPin,
                            RoleID = (int)cbRoles.SelectedValue
                        };
                        db.Employees.Add(newEmp);
                    }
                    else
                    {
                        // Редактирование существующего
                        var empToUpdate = db.Employees.Find(_selectedEmployee.EmployeeID);
                        if (empToUpdate != null)
                        {
                            empToUpdate.FullName = txtFullName.Text.Trim();
                            empToUpdate.PinCode = newPin;
                            empToUpdate.RoleID = (int)cbRoles.SelectedValue;
                        }
                    }

                    db.SaveChanges();
                }

                MessageBox.Show("Данные сотрудника успешно сохранены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                btnClear_Click(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Удаление сотрудника
        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedEmployee == null) return;

            // Запрет на удаление самого себя (чтобы админ случайно не удалил свою учетку и не потерял доступ)
            if (_selectedEmployee.EmployeeID == AppState.CurrentUser.EmployeeID)
            {
                MessageBox.Show("Вы не можете удалить собственную учетную запись!", "Защита", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"Вы действительно хотите удалить сотрудника '{_selectedEmployee.FullName}'?",
                                         "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var db = new AutoCashierDbEntities1())
                    {
                        var empToDel = db.Employees.Find(_selectedEmployee.EmployeeID);
                        if (empToDel != null)
                        {
                            db.Employees.Remove(empToDel);
                            db.SaveChanges();
                        }
                    }
                    MessageBox.Show("Сотрудник удален.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                    btnClear_Click(null, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Невозможно удалить сотрудника. Возможно, он привязан к существующим кассовым сменам или чекам.\nДетали: {ex.Message}",
                                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}