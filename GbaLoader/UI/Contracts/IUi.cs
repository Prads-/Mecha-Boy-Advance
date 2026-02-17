namespace GbaLoader.UI.Contracts;

using Terminal.Gui;
using static Terminal.Gui.View;

internal interface IUi
{
    void ShowUi(Window window);
    void ProcessInput(KeyEventEventArgs args);
}