using System.Windows.Automation;

namespace WpfApp4.Services
{
    public class BrowserMonitorService : IBrowserMonitorService
    {
        public string? GetCurrentUrl()
        {
            try
            {
                var root = AutomationElement.RootElement;

                var condition = new OrCondition(
                    new PropertyCondition(AutomationElement.NameProperty, "주소창 및 검색창"),
                    new PropertyCondition(AutomationElement.NameProperty, "Address and search bar"));

                var element = root.FindFirst(TreeScope.Descendants, condition);

                if (element == null) return null;
                if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out object patternObj)) return null;

                return ((ValuePattern)patternObj).Current.Value;
            }
            catch
            {
                return null;
            }
        }
    }
}
