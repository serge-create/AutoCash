using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AutoCash.Core;
using AutoCash.Models; // Твои сгенерированные классы EF

namespace AutoCash.Views.Management
{
    public partial class ProductManager : Window
    {
        private List<Products> _allProducts = new List<Products>();
        private Products _selectedProduct = null;

        public ProductManager()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
            ApplyRoleSecurity();
        }

        // Загрузка данных из БД с использованием Жадной загрузки (Include)
        private void LoadData()
        {
            try
            {
                using (var db = new Models.AutoCashierDbEntities1()) // Если контекст называется иначе, замени на свое имя класса контекста
                {
                    // Подгружаем связанные сущности групп и единиц измерения
                    _allProducts = db.Products
                                             .Where(p => p.IsDeleted == false || p.IsDeleted == null)
                                             .Include(p => p.Product_Groups)
                                             .Include(p => p.MeasureUnits)
                                             .ToList();

                    // Заполняем выпадающие списки в карточке
                    cbCardGroup.ItemsSource = db.Product_Groups.ToList();
                    cbCardUnit.ItemsSource = db.MeasureUnits.ToList();
                    cbVATRate.ItemsSource = db.Tax_Rates.ToList();
                    // Заполняем фильтр групп (+ добавляем пустой элемент "Все группы")
                    var groupsForFilter = db.Product_Groups.ToList();
                    groupsForFilter.Insert(0, new Product_Groups { GroupID = -1, Name = "Все категории" });
                    cbGroups.ItemsSource = groupsForFilter;
                    cbGroups.SelectedIndex = 0;
                }
                RefreshGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Разграничение прав внутри окна
        private void ApplyRoleSecurity()
        {
            string currentRole = AppState.CurrentUser.Roles?.RoleName;

            // "Старший кассир" может смотреть и менять цены, но не может УДАЛЯТЬ товары
            if (currentRole == "Старший кассир")
            {
                btnDelete.IsEnabled = false;
                btnDelete.ToolTip = "Удаление товаров доступно только Администратору.";
            }
        }

        // Логика фильтрации и поиска (LINQ to Objects)
        private void RefreshGrid()
        {
            var filtered = _allProducts.AsEnumerable();

            // Поиск по тексту
            if (!string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                string search = txtSearch.Text.ToLower().Trim();
                filtered = filtered.Where(p => p.Name.ToLower().Contains(search) ||
                                               (p.Barcode != null && p.Barcode.Contains(search)));
            }

            // Фильтр по категории
            if (cbGroups.SelectedItem is Product_Groups selectedGroup && selectedGroup.GroupID != -1)
            {
                filtered = filtered.Where(p => p.GroupID == selectedGroup.GroupID);
            }

            dgProducts.ItemsSource = filtered.ToList();
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e) => RefreshGrid();
        private void cbGroups_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshGrid();

        private void btnResetFilter_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = string.Empty;
            cbGroups.SelectedIndex = 0;
        }


        // Выбор товара в таблице -> Заполнение карточки справа
        private void dgProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgProducts.SelectedItem is Products product)
            {
                _selectedProduct = product;
                txtBarcode.Text = product.Barcode;
                txtProductName.Text = product.Name;
                txtPrice.Text = product.Price.ToString("F2");
                txtStock.Text = product.StockQuantity.ToString();
                cbCardGroup.SelectedValue = product.GroupID;
                cbCardUnit.SelectedValue = product.MeasureUnitID;
                cbVATRate.SelectedValue = product.VATRateID;

                lblCardTitle.Text = "Редактирование товара";
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            _selectedProduct = null;
            txtBarcode.Clear();
            txtProductName.Clear();
            txtPrice.Clear();
            txtStock.Clear();
            cbCardGroup.SelectedIndex = -1;
            cbCardUnit.SelectedIndex = -1;
            lblCardTitle.Text = "Новый товар";
            txtArticle.Clear();
            txtDiscount.Clear();
            cbVATRate.SelectedIndex = -1;
        }

        // Добавление / Изменение товара в БД
        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            // Базовая валидация полей
            if (string.IsNullOrWhiteSpace(txtProductName.Text) || !decimal.TryParse(txtPrice.Text, out decimal price) || price <= 0 || cbVATRate.SelectedValue == null)
            {
                MessageBox.Show("Заполните корректно Наименование, Цену и выберите ставку НДС!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!decimal.TryParse(txtDiscount.Text, out decimal discount))
            {
                MessageBox.Show("Заполните корректно Скидку!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                using (var db = new Models.AutoCashierDbEntities1())
                {
                    if (_selectedProduct == null) // Добавление нового товара
                    {
                        var newProduct = new Products
                        {
                            Article = txtArticle.Text.Trim(),           // НОВОЕ ПОЛЕ
                            DiscountPercent = discount,                 // НОВОЕ ПОЛЕ
                            VATRateID = (int)cbVATRate.SelectedValue,
                            Barcode = txtBarcode.Text,
                            Name = txtProductName.Text,
                            Price = price,
                            StockQuantity = int.TryParse(txtStock.Text, out int st) ? st : 0,
                            GroupID = (int?)cbCardGroup.SelectedValue,
                            MeasureUnitID = cbCardUnit.SelectedValue != null ? Convert.ToInt32(cbCardUnit.SelectedValue) : 0
                        };
                        db.Products.Add(newProduct);
                    }
                    else // Редактирование существующего
                    {
                        var productToUpdate = db.Products.Find(_selectedProduct.ProductID);
                        if (productToUpdate != null)
                        {
                            productToUpdate.Article = txtArticle.Text.Trim();
                            productToUpdate.DiscountPercent = discount;
                            productToUpdate.VATRateID = (int)cbVATRate.SelectedValue;
                            productToUpdate.Barcode = txtBarcode.Text;
                            productToUpdate.Name = txtProductName.Text;
                            productToUpdate.Price = price;
                            productToUpdate.StockQuantity = int.TryParse(txtStock.Text, out int st) ? st : 0;
                            productToUpdate.GroupID = (int?)cbCardGroup.SelectedValue;
                            productToUpdate.MeasureUnitID = cbCardUnit.SelectedValue != null ? Convert.ToInt32(cbCardUnit.SelectedValue) : 0;
                        }
                    }

                    db.SaveChanges();
                }

                MessageBox.Show("Данные успешно сохранены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData(); // Перезагружаем кэш данных из БД
                btnClear_Click(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Удаление товара (доступно только Admin)
        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProduct == null) return;

            var res = MessageBox.Show($"Вы уверены, что хотите удалить товар '{_selectedProduct.Name}'?\n\nОн будет перенесен в архив (мягкое удаление).",
                                      "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                try
                {
                    using (var db = new Models.AutoCashierDbEntities1())
                    {
                        var product = db.Products.Find(_selectedProduct.ProductID); // Ищем по ProductID
                        if (product != null)
                        {
                            // РЕАЛИЗАЦИЯ МЯГКОГО УДАЛЕНИЯ
                            product.IsDeleted = true;  // Помечаем как удаленный
                            product.StockQuantity = 0; // Обнуляем остатки на складе, чтобы не портить инвентаризацию

                            db.SaveChanges();
                        }
                    }
                    MessageBox.Show("Товар успешно перенесен в архив.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                    btnClear_Click(null, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при архивации товара: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnAddGroup_Click(object sender, RoutedEventArgs e)
        {
            AddCategoryWindow addCatWindow = new AddCategoryWindow();
            addCatWindow.Owner = this; // Вот так правильно задается владелец окна

            if (addCatWindow.ShowDialog() == true)
            {
                // Если категория успешно добавлена, обновляем источники данных ComboBox
                try
                {
                    using (var db = new AutoCashierDbEntities1())
                    {
                        var updatedGroups = db.Product_Groups.ToList();

                        // 1. Обновляем выпадающий список в карточке редактирования товара
                        cbCardGroup.ItemsSource = updatedGroups;

                        // 2. Обновляем верхний фильтр категорий (сохраняя элемент "Все категории")
                        var groupsForFilter = updatedGroups.ToList();
                        groupsForFilter.Insert(0, new Product_Groups { GroupID = -1, Name = "Все категории" });
                        cbGroups.ItemsSource = groupsForFilter;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка обновления списков: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}