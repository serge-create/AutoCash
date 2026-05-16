using AutoCash.Models;
using AutoCash.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoCash.Core
{
    /// <summary>
    /// Глобальный класс для хранения состояния текущего сеанса работы приложения.
    /// Обеспечивает доступ к данным авторизованного пользователя из любого окна.
    /// </summary>
    public static class AppState
    {
        public class Employee
        {
            [Key] // Указывает, что это первичный ключ (EmployeeID в базе)
            public int EmployeeID { get; set; }

            public string FullName { get; set; }
            public string Login { get; set; }
            public string Password { get; set; }

            // Поле роли: "Администратор", "Старший кассир", "Кассир"
            public string Role { get; set; }
        }

        /// <summary>
        /// Текущий авторизованный пользователь (сотрудник)
        /// </summary>
        public static Employees CurrentUser { get; set; }

        /// <summary>
        /// Метод для очистки состояния при выходе из системы (Смена пользователя)
        /// </summary>
        public static void Logout()
        {
            CurrentUser = null;
            // Смену не очищаем, так как при смене кассира (например, один отошел, другой сел) 
            // кассовая смена обычно остается открытой на магазине.
        }
    }
}
