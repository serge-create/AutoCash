using AutoCash.Core;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AutoCash.Core;   // Подключаем наш AppState
using AutoCash.Models; // Подключаем сгенерированные классы БД (EF Core)

namespace AutoCash.Views

{
    /// <summary>
    /// Логика взаимодействия для LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Page
    {
        
        public LoginWindow()
        {
            InitializeComponent();
            txtLogin.Focus();
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string login = txtLogin.Text.Trim();
            string password = txtPassword.Password;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Заполните поля 'Логин' и 'Пароль'!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Открываем контекст, сгенерированный базой данных
                using (var db = new Models.AutoCashierDbEntities())
                {
                    // Делаем запрос к таблице Employees.
                    // .Include(emp => emp.Roles) — автоматически подгружает данные из таблицы Roles 
                    // на основе внешнего ключа (FK), который был настроен в SQL-скрипте.
                    var employee = db.Employees
                                     .Include(emp => emp.Roles)
                                     .FirstOrDefault(emp => emp.PinCode == login && emp.INN == password);

                    if (employee != null)
                    {
                        // Авторизация успешна. Сохраняем объект сотрудника со всеми его авто-свойствами
                        AppState.CurrentUser = employee;

                        // Переходим в главное окно программы
                        MainWindow mainWindow = new MainWindow();
                        Window currentWindow = Window.GetWindow(this);
                        if (currentWindow != null)
                        {
                            currentWindow.Close();
                        }
                        mainWindow.Show();
                    }
                    else
                    {
                        MessageBox.Show("Неверный логин или пароль сотрудника!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        txtPassword.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                // На случай, если забыли запустить локальный SQL Server или строка подключения неверна
                MessageBox.Show($"Ошибка доступа к SQL Server:\n{ex.Message}", "Системный сбой", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
