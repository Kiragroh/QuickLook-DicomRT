using System;
using System.Windows;
using System.Windows.Controls;

namespace QuickLook.DicomRT
{
    public sealed partial class ViewerControl
    {
        private Action keepOpenAction;
        private readonly Button keepOpenButton=Theme.Button("Keep open");
        private Button BuildKeepOpenButton()
        {
            keepOpenButton.Visibility=Visibility.Collapsed;
            keepOpenButton.Click+=(s,e)=>RequestKeepOpen();
            return keepOpenButton;
        }
        public void SetKeepOpenAction(Action action)
        {
            keepOpenAction=action;keepOpenButton.Visibility=Visibility.Visible;
            keepOpenButton.IsEnabled=action!=null;
            keepOpenButton.Content=action!=null?"Keep open":"Kept open";
            keepOpenButton.ToolTip=action!=null?"Keep this case in a separate window. Other QuickLook previews will not replace it (Ctrl+Shift+K).":"Independent window: close it normally when finished. Other previews do not change this case.";
            ToolTipService.SetShowOnDisabled(keepOpenButton,true);
        }
        private void RequestKeepOpen(){if(!disposed)keepOpenAction?.Invoke();}
    }
}
