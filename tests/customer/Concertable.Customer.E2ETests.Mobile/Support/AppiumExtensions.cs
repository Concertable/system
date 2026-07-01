using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace Concertable.Customer.E2ETests.Mobile.Support;

public static class AppiumExtensions
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    public static AppiumElement GetByTestId(this AndroidDriver driver, string testId) =>
        driver.WaitFor(d => d.FindElement(MobileBy.AccessibilityId(testId)), DefaultTimeout);

    public static AppiumElement GetByTestId(this AndroidDriver driver, string testId, TimeSpan timeout) =>
        driver.WaitFor(d => d.FindElement(MobileBy.AccessibilityId(testId)), timeout);

    public static bool TryGetByTestId(this AndroidDriver driver, string testId, TimeSpan timeout, out AppiumElement? element)
    {
        try
        {
            element = driver.WaitFor(d => d.FindElement(MobileBy.AccessibilityId(testId)), timeout);
            return true;
        }
        catch (WebDriverTimeoutException)
        {
            element = null;
            return false;
        }
    }

    public static void WaitUntilGone(this AndroidDriver driver, string testId, TimeSpan? timeout = null)
    {
        var wait = new DefaultWait<AndroidDriver>(driver)
        {
            Timeout = timeout ?? DefaultTimeout,
            PollingInterval = TimeSpan.FromMilliseconds(250),
        };
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        wait.Until(d =>
        {
            try
            {
                d.FindElement(MobileBy.AccessibilityId(testId));
                return false;
            }
            catch (NoSuchElementException)
            {
                return true;
            }
        });
    }

    private static AppiumElement WaitFor(this AndroidDriver driver, Func<AndroidDriver, AppiumElement> finder, TimeSpan timeout)
    {
        var wait = new DefaultWait<AndroidDriver>(driver)
        {
            Timeout = timeout,
            PollingInterval = TimeSpan.FromMilliseconds(250),
        };
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
        return wait.Until(finder);
    }
}
