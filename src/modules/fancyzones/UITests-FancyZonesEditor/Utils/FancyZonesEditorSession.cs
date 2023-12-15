// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;

namespace Microsoft.FancyZonesEditor.UnitTests.Utils
{
    public class FancyZonesEditorSession
    {
        protected const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";
        private const string FancyZonesEditorName = "PowerToys.FancyZonesEditor";
        private const string FancyZonesEditorPath = @"\..\..\..\" + FancyZonesEditorName + ".exe";
        private TestContext context;

        public static class AccessibilityId
        {
            // main window
            public const string MainWindow = "MainWindow1";
            public const string Monitors = "Monitors";
            public const string NewLayoutButton = "NewLayoutButton";

            // layout card
            public const string EditLayoutButton = "EditLayoutButton";

            // edit layout window: common for template and custom layouts
            public const string SensitivitySlider = "SensitivityInput";
            public const string SpacingSlider = "Spacing";
            public const string SpacingToggle = "spaceAroundSetting";

            // edit template layout window
            public const string CopyTemplate = "createFromTemplateLayoutButton";
            public const string TemplateZoneSlider = "TemplateZoneCount";

            // edit custom layout window
            public const string DuplicateLayoutButton = "duplicateLayoutButton";
            public const string DeleteLayoutButton = "deleteLayoutButton";
            public const string KeySelectionComboBox = "quickKeySelectionComboBox";
            public const string EditZonesButton = "editZoneLayoutButton";
            public const string DeleteTextButton = "DeleteButton";
            public const string HotkeyComboBox = "quickKeySelectionComboBox";
        }

        public WindowsDriver<WindowsElement>? Session { get; }

        public FancyZonesEditorSession(TestContext testContext)
        {
            try
            {
                // Launch FancyZonesEditor
                string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                path += FancyZonesEditorPath;

                AppiumOptions opts = new AppiumOptions();
                opts.AddAdditionalCapability("app", path);
                Session = new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), opts);
            }
            catch (Exception ex)
            {
                testContext.WriteLine(ex.Message);
            }

            Assert.IsNotNull(Session, "Session not initialized");

            // Set implicit timeout to 1.5 seconds to make element search to retry every 500 ms for at most three times
            Session.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1.5);

            context = testContext;
        }

        public void Close()
        {
            // Close the session
            if (Session != null)
            {
                try
                {
                    // in case if something went wrong and an error message is shown
                    var dialog = Session.FindElementByName("Editor data parsing error.");
                    Session.CloseApp(); // will close the dialog

                    // session can't access new Editor instance created after closing the dialog
                    // kill the process
                    IntPtr appTopLevelWindowHandle = IntPtr.Zero;
                    foreach (Process clsProcess in Process.GetProcesses())
                    {
                        if (clsProcess.ProcessName.Equals(FancyZonesEditorName, StringComparison.OrdinalIgnoreCase))
                        {
                            clsProcess.Kill();
                            break;
                        }
                    }
                }
                catch
                {
                }

                try
                {
                    // FZEditor application can be closed by explicitly closing main editor window
                    var mainEditorWindow = Session.FindElementByAccessibilityId(AccessibilityId.MainWindow);
                    mainEditorWindow?.SendKeys(Keys.Alt + Keys.F4);
                }
                catch (Exception ex)
                {
                    context.WriteLine("Unable to close main window. ", ex.Message);
                }

                Session.Quit();
                Session.Dispose();
            }
        }

        public WindowsElement? GetLayout(string layoutName)
        {
            try
            {
                var listItem = Session?.FindElementByName(layoutName);
                return listItem;
            }
            catch (Exception)
            {
                context.WriteLine("Layout " + layoutName + " not found");
                return null;
            }
        }

        public WindowsElement? OpenContextMenu(string layoutName)
        {
            RightClick_Layout(layoutName);
            var menu = Session?.FindElementByClassName("ContextMenu");
            Assert.IsNotNull(menu, "Context menu not found");
            return menu;
        }

        public WindowsElement? GetMonitorsList()
        {
            return FindByAccessibilityId(AccessibilityId.Monitors);
        }

        public WindowsElement GetMonitorItem(int monitorNumber)
        {
            try
            {
                var monitorsList = GetMonitorsList();
                Assert.IsNotNull(monitorsList, "Monitors list not found");
                var listItem = monitorsList?.FindElementByName($"{monitorNumber}");
                Assert.IsNotNull(listItem, "Monitor " + monitorNumber + " not found");
                return (WindowsElement)listItem;
            }
            catch (Exception)
            {
                Assert.Fail("Monitor " + monitorNumber + " not found");
                return null;
            }
        }

        public WindowsElement? GetZoneCountSlider()
        {
            return FindByAccessibilityId(AccessibilityId.TemplateZoneSlider);
        }

        public WindowsElement? GetSensitivitySlider()
        {
            return FindByAccessibilityId(AccessibilityId.SensitivitySlider);
        }

        public WindowsElement? GetSpaceAroundZonesSlider()
        {
            return FindByAccessibilityId(AccessibilityId.SpacingSlider);
        }

        public WindowsElement? GetSpaceAroundZonesToggle()
        {
            return FindByAccessibilityId(AccessibilityId.SpacingToggle);
        }

        public WindowsElement? GetNameInput()
        {
            try
            {
                return Session?.FindElementByClassName("TextBox");
            }
            catch
            {
                Assert.Fail($"Name TextBox not found");
                return null;
            }
        }

        public WindowsElement? GetHotkeyComboBox()
        {
            return FindByAccessibilityId(AccessibilityId.HotkeyComboBox);
        }

        public WindowsElement? GetHotkeyPopup()
        {
            try
            {
                return Session?.FindElementByClassName("Popup");
            }
            catch
            {
                Assert.Fail($"Hotkey popup window not found");
                return null;
            }
        }

        public void Click_CreateNewLayout()
        {
            var button = FindByAccessibilityId(AccessibilityId.NewLayoutButton);
            Assert.IsNotNull(button, "Create new layout button not found");
            button?.Click();
        }

        public void Click_EditLayout(string layoutName)
        {
            var layout = GetLayout(layoutName);
            Assert.IsNotNull(layout, $"Layout \"{layoutName}\" not found");

            // added retry attempts, because Click can fail for some reason
            bool opened = false;
            int retryAttempts = 10;
            while (!opened && retryAttempts > 0)
            {
                var editButton = layout?.FindElementByAccessibilityId(AccessibilityId.EditLayoutButton);
                Assert.IsNotNull(editButton, "Edit button not found");
                editButton.Click();

                // wait until the dialog is opened
                opened = WaitElementDisplayedByName($"Edit '{layoutName}'");
                retryAttempts--;
            }
        }

        public void RightClick_Layout(string layoutName)
        {
            var layout = GetLayout(layoutName);
            ContextClick(layout!);
        }

        public void Click_Monitor(int monitorNumber)
        {
            var monitor = GetMonitorItem(monitorNumber);
            Click(monitor!);
        }

        public void Click_Save()
        {
            var button = Session?.FindElementByName("Save");
            Assert.IsNotNull(button, "No Save button");
            button.Click();
        }

        public void Click_Cancel()
        {
            var button = Session?.FindElementByName("Cancel");
            Assert.IsNotNull(button, "No Cancel button");
            button.Click();
        }

        private WindowsElement? FindByAccessibilityId(string name)
        {
            try
            {
                return Session?.FindElementByAccessibilityId(name);
            }
            catch (Exception)
            {
                Assert.Fail($"{name} not found");
                return null;
            }
        }

        public bool WaitElementDisplayedByName(string name)
        {
            try
            {
                WebDriverWait wait = new WebDriverWait(Session, TimeSpan.FromSeconds(1));
                return wait.Until(pred =>
                {
                    var element = Session?.FindElementByName(name);
                    if (element != null)
                {
                    return element.Displayed;
                }

                    return false;
                });
            }
            catch
            {
                return false;
            }
        }

        public void WaitElementDisplayedById(string id)
        {
            WebDriverWait wait = new WebDriverWait(Session, TimeSpan.FromSeconds(1));
            wait.Until(pred =>
            {
                var element = Session?.FindElementByAccessibilityId(id);
                if (element != null)
                {
                    return element.Displayed;
                }

                return false;
            });
        }

        public void WaitUntilHidden(WindowsElement element)
        {
            WebDriverWait wait = new WebDriverWait(Session, TimeSpan.FromSeconds(0.5));
            wait.Until(pred =>
            {
                return !element.Displayed;
            });
        }

        public void ContextClick(WindowsElement element)
        {
            Actions actions = new Actions(Session);
            actions.MoveToElement(element);
            actions.MoveByOffset(10, 10);
            actions.ContextClick();
            actions.Build().Perform();
        }

        public void Click(AppiumWebElement element)
        {
            Actions actions = new Actions(Session);
            actions.MoveToElement(element);
            actions.MoveByOffset(10, 10);
            actions.Click();
            actions.Build().Perform();
        }
    }
}
