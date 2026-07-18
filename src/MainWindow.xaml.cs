using Prism.Events;
using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Quake2TextureConverter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// ViewModel.
        /// </summary>
        public MainWindowViewModel ViewModel => DataContext as MainWindowViewModel;

        /// <summary>
        /// Constructor.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Prism constructor.
        /// </summary>
        /// <param name="eventAggregator"></param>
        public MainWindow(IEventAggregator eventAggregator) : this()
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<LogMessagesScrollToEndEvent>().Subscribe(
                HandleLogMessagesScrollToEndEvent);
        }

        private void HandleLogMessagesScrollToEndEvent()
        {
            LogMessagesTextBox.ScrollToEnd();
        }

        // Private variables.
        private IEventAggregator _eventAggregator;
    }
}