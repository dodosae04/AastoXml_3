using System.Windows;
using AasExcelToXml.Wpf.ViewModels;

namespace AasExcelToXml.Wpf;

// [역할] 앱 전역 설정(언어/폴더 기억/카테고리 옵션 등)을 편집하는 대화상자를 표시한다.
// [입력] SettingsViewModel로 전달된 현재 설정 값.
// [출력] 저장 여부(DialogResult)만 반환하고 실제 저장은 MainWindow/SettingsService가 수행한다.
// [수정 포인트] UI 컨트롤 바인딩 키를 변경할 경우 ViewModel 속성과 함께 맞춰야 한다.
public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
