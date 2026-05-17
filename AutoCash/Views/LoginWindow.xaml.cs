using System;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AutoCash.Core;
using AutoCash.Models; // Твои классы БД

namespace AutoCash.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            // Фокус сразу на поле пароля, чтобы кассир мог сразу вводить цифры/текст
            txtPassword.Focus();
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            PerformAuthentication();
        }

        private void PerformAuthentication()
        {
            lblError.Visibility = Visibility.Collapsed;
            string password = txtPassword.Password.Trim();

            if (string.IsNullOrEmpty(password))
            {
                ShowErrorMessage("Введите пароль!");
                return;
            }

            try
            {
                using (var db = new AutoCashierDbEntities1()) // Проверь, что имя контекста верное
                {
                    // ==========================================
                    // РЕЖИМ ПЕРВИЧНОЙ ИНИЦИАЛИЗАЦИИ (БАЗА ПУСТА)
                    // ==========================================
                    if (!db.Employees.Any())
                    {
                        if (password == "123456789")
                        {
                            // Проверяем, существует ли роль "Администратор", если нет - создаем
                            var adminRole = db.Roles.FirstOrDefault(r => r.RoleName == "Администратор");
                            if (adminRole == null)
                            {
                                adminRole = new Roles { RoleName = "Администратор" };
                                db.Roles.Add(adminRole);
                                db.SaveChanges(); // Сохраняем, чтобы база присвоила роли ID
                            }

                            // Создаем дефолтного суперпользователя
                            var defaultAdmin = new Employees
                            {
                                FullName = "Системный Администратор",
                                PinCode = "123456789",
                                RoleID = adminRole.RoleID // Убедись, что свойство ID роли называется именно так (RoleID или ID)
                            };

                            db.Employees.Add(defaultAdmin);
                            db.SaveChanges();

                            // Выводим строгое уведомление
                            MessageBox.Show("Первичная инициализация прошла успешно.\nСоздан профиль Системного Администратора.\n\nВНИМАНИЕ: Обязательно перейдите в раздел 'Персонал' и измените этот стандартный ПИН-код (123456789) в целях безопасности!",
                                            "Безопасность системы", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        else
                        {
                            ShowErrorMessage("База пользователей пуста. Введите стандартный пароль для инициализации.");
                            txtPassword.Clear();
                            return;
                        }
                    }

                    // ==========================================
                    // СТАНДАРТНАЯ ЛОГИКА ВХОДА
                    // ==========================================
                    var employee = db.Employees
                                     .Include(e => e.Roles) // Жадная загрузка роли
                                     .FirstOrDefault(e => e.PinCode == password);

                    if (employee != null)
                    {
                        // Успешная авторизация
                        AppState.CurrentUser = employee;

                        MainWindow mainWindow = new MainWindow();
                        mainWindow.Show();
                        this.Close();
                    }
                    else
                    {
                        // Если пароль не найден в базе
                        ShowErrorMessage("Сотрудник с таким паролем не найден!");
                        txtPassword.Clear();
                        txtPassword.Focus();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения к базе данных:\n{ex.Message}",
                                "Критический сбой", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ShowErrorMessage(string message)
        {
            lblError.Text = message;
            lblError.Visibility = Visibility.Visible;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Поддержка нажатия Enter для входа
            if (e.Key == Key.Enter)
            {
                PerformAuthentication();
            }
        }
    }
}