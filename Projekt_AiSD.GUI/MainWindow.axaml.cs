// using Avalonia.Controls;

// namespace Projekt_AiSD.GUI
// {
//     public partial class MainWindow : Window
//     {
//         public MainWindow()
//         {
//             InitializeComponent();
//         }
//     }
// }

using Avalonia.Controls;
using Projekt_AiSD.Models;

namespace Projekt_AiSD.GUI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(UniversityData daneUczelni)
        {
            InitializeComponent();

            if (daneUczelni != null && daneUczelni.Instructors != null)
            {
                var tabela = this.FindControl<DataGrid>("PlanDataGrid");
                if (tabela != null)
                {
                    tabela.ItemsSource = daneUczelni.Instructors;
                }
            }
        }
    }
}