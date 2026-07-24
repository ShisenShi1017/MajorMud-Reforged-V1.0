using System.Windows; using System.Windows.Controls;
namespace Reforged.App.Views;
public sealed class PlaceholderView : UserControl { public PlaceholderView(string title,string description){Content=new StackPanel{Margin=new Thickness(24),Children={new TextBlock{Text=title,FontSize=28,FontWeight=FontWeights.Bold},new TextBlock{Text=description,Margin=new Thickness(0,12,0,0),TextWrapping=TextWrapping.Wrap}}};} }
