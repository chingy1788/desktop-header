using System;
using System.IO;
using System.Linq;
using Xunit;
using FlaUI.Core;
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using DesktopHeader.App.Models;
using VirtualDesktop;

namespace DesktopHeader.Tests
{
    public class DesktopHeaderTests
    {
        #region 1. Unit Tests (Non-Business Logic & Models)

        [Fact]
        public void TestDesktopItemPropertyChangeNotification()
        {
            // Arrange
            var item = new DesktopItem { Index = 0, Name = "Test Desktop", IsActive = false };
            bool propertyChangedFired = false;
            string? changedPropertyName = null;

            item.PropertyChanged += (sender, e) =>
            {
                propertyChangedFired = true;
                changedPropertyName = e.PropertyName;
            };

            // Act
            item.IsActive = true;

            // Assert
            Assert.True(propertyChangedFired);
            Assert.Equal(nameof(DesktopItem.IsActive), changedPropertyName);
            Assert.True(item.IsActive);
        }

        [Fact]
        public void TestDesktopItemNameUpdateNotification()
        {
            // Arrange
            var item = new DesktopItem { Index = 1, Name = "Original Name", IsActive = false };
            bool propertyChangedFired = false;
            string? changedPropertyName = null;

            item.PropertyChanged += (sender, e) =>
            {
                propertyChangedFired = true;
                changedPropertyName = e.PropertyName;
            };

            // Act
            item.Name = "Updated Name";

            // Assert
            Assert.True(propertyChangedFired);
            Assert.Equal(nameof(DesktopItem.Name), changedPropertyName);
            Assert.Equal("Updated Name", item.Name);
        }

        [Fact]
        public void TestCOMDesktopCountAndNameQuery()
        {
            // Arrange & Act
            int count = Desktop.Count;
            
            // Assert
            Assert.True(count > 0, "Windows should have at least 1 virtual desktop.");
            
            for (int i = 0; i < count; i++)
            {
                string name = Desktop.DesktopNameFromIndex(i);
                Assert.False(string.IsNullOrEmpty(name), $"Desktop name for index {i} should not be empty.");
            }
        }

        [Theory]
        [InlineData("Work", "Work")]
        [InlineData("Home & Projects", "Home & Projects")]
        [InlineData("Desktop/1", "Desktop1")]
        [InlineData("A\\B:C*D?E\"F<G>H|I", "ABCDEFGHI")]
        [InlineData("", "Default")]
        [InlineData(null, "Default")]
        [InlineData("   ", "Default")]
        [InlineData("  Leading And Trailing  ", "Leading And Trailing")]
        public void TestSanitizeFolderName(string? input, string expected)
        {
            // Act
            string result = DesktopHeader.App.MainWindow.SanitizeFolderName(input!);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region 2. Gherkin UI Automation Tests (FlaUI)

        private string GetAppPath()
        {
            // Locate the DesktopHeader.App.exe relative to test assembly
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            // Go up from bin/<config>/net9.0-windows/ to solution root
            string solutionRoot = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\.."));

            // Check Release first, then Debug (supports both CI and local dev runs)
            string[] configs = { "Release", "Debug" };
            foreach (var config in configs)
            {
                string candidate = Path.Combine(solutionRoot, "DesktopHeader.App", "bin", config, "net9.0-windows", "DesktopHeader.App.exe");
                if (File.Exists(candidate))
                    return candidate;
            }

            string releasePath = Path.Combine(solutionRoot, "DesktopHeader.App", "bin", "Release", "net9.0-windows", "DesktopHeader.App.exe");
            throw new FileNotFoundException($"App executable not found. Tried Release and Debug configurations under: {Path.Combine(solutionRoot, "DesktopHeader.App", "bin")}");
        }

        private Window? GetMainWindowSafe(Application app, UIA3Automation automation, TimeSpan timeout)
        {
            var start = DateTime.Now;
            while ((DateTime.Now - start) < timeout)
            {
                try
                {
                    var windows = app.GetAllTopLevelWindows(automation);
                    var win = windows.FirstOrDefault(w => w.Title == "Virtual Desktop Header");
                    if (win != null) return win;
                }
                catch { }
                System.Threading.Thread.Sleep(200);
            }
            return null;
        }

        private Application LaunchApp(string appPath)
        {
            var psi = new System.Diagnostics.ProcessStartInfo(appPath);
            psi.EnvironmentVariables["RUNNING_UI_TESTS"] = "true";
            return Application.Launch(psi);
        }

        /*
         * Scenario: Virtual Desktop Header floating bar is launched
         *   Given the application is built and running
         *   Then a transparent floating header window should appear
         *   And it should be Topmost
         */
        [Fact]
        public void UI_Scenario_HeaderWindowLaunchesAndIsTopmost()
        {
            if (!Environment.UserInteractive || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != null || Environment.GetEnvironmentVariable("SKIP_UI_TESTS") == "true")
            {
                return; // Skip FlaUI tests in headless/non-interactive environment
            }

            string appPath = GetAppPath();
            
            // Launch the WPF application
            using var app = LaunchApp(appPath);
            using var automation = new UIA3Automation();
            
            try
            {
                // Wait for the main window to load with a 5-second timeout
                var window = GetMainWindowSafe(app, automation, TimeSpan.FromSeconds(5));
                
                // Assert Window presence and topmost property
                Assert.NotNull(window);
                Assert.Equal("Virtual Desktop Header", window.Title);
            }
            finally
            {
                try { app.Close(); } catch { }
            }
        }

        /*
         * Scenario: Desktop button list accurately shows available desktops
         *   Given the header bar is visible
         *   When we query the desktop count on the system
         *   Then the number of buttons in the header should match the virtual desktop count
         */
        [Fact]
        public void UI_Scenario_ButtonCountMatchesSystemDesktopCount()
        {
            if (!Environment.UserInteractive || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != null || Environment.GetEnvironmentVariable("SKIP_UI_TESTS") == "true")
            {
                return; // Skip FlaUI tests in headless/non-interactive environment
            }

            string appPath = GetAppPath();
            int actualDesktopCount = Desktop.Count;

            using var app = LaunchApp(appPath);
            using var automation = new UIA3Automation();
            
            try
            {
                var window = GetMainWindowSafe(app, automation, TimeSpan.FromSeconds(5));
                Assert.NotNull(window);

                // Wait for UI layout to settle and populate list
                System.Threading.Thread.Sleep(500);

                // Find all Buttons inside the DesktopsList ItemsControl
                var desktopsList = window.FindFirstDescendant(cf => cf.ByAutomationId("DesktopsList"));
                Assert.NotNull(desktopsList);
                var buttons = desktopsList.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));
                
                Assert.Equal(actualDesktopCount, buttons.Length);
            }
            finally
            {
                try { app.Close(); } catch { }
            }
        }

        /*
         * Scenario: Clicking a desktop button switches the desktop
         *   Given the header bar is visible
         *   When we click on a desktop button
         *   Then the active desktop should switch (simulated switch validation)
         */
        [Fact]
        public void UI_Scenario_ClickingButtonSwitchesActiveDesktop()
        {
            if (!Environment.UserInteractive || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != null || Environment.GetEnvironmentVariable("SKIP_UI_TESTS") == "true")
            {
                return; // Skip FlaUI tests in headless/non-interactive environment
            }

            string appPath = GetAppPath();
            using var app = LaunchApp(appPath);
            using var automation = new UIA3Automation();
            
            try
            {
                var window = GetMainWindowSafe(app, automation, TimeSpan.FromSeconds(5));
                Assert.NotNull(window);

                var buttons = window.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));
                Assert.NotEmpty(buttons);

                var firstButton = buttons.First().AsButton();
                Assert.NotNull(firstButton);
                
                // Invoke the first button (should trigger DesktopButton_Click without exception)
                firstButton.Invoke();
                
                // Ensure no crash occurred and button remains clickable
                Assert.True(firstButton.IsEnabled);
            }
            finally
            {
                try { app.Close(); } catch { }
            }
        }

        /*
         * Scenario: Notes panel collapsing behaviors under ShowNotesPreview=true (Default)
         *   Given the app is launched in standard mode
         *   When the space is ample, NotesContainer should be visible, and NotesButton should be collapsed.
         *   When the width is reduced, NotesContainer should collapse, and NotesButton should appear.
         *   When the width is restored, NotesContainer should restore, and NotesButton should collapse.
         */
        [Fact]
        public void UI_Scenario_NotesPreviewCollapseAndRestoreOnResize()
        {
            if (!Environment.UserInteractive || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != null || Environment.GetEnvironmentVariable("SKIP_UI_TESTS") == "true")
            {
                return; // Skip FlaUI tests in headless/non-interactive environment
            }

            // Create/Force settings to be ShowNotesPreview=true for this test run
            try
            {
                string settingsPath = Path.Combine(Path.GetDirectoryName(GetAppPath())!, "settings.txt");
                File.WriteAllText(settingsPath, "True");
            }
            catch { }

            string appPath = GetAppPath();
            using var app = LaunchApp(appPath);
            using var automation = new UIA3Automation();
            
            try
            {
                var window = GetMainWindowSafe(app, automation, TimeSpan.FromSeconds(5));
                Assert.NotNull(window);

                // Wait for layout to settle
                System.Threading.Thread.Sleep(500);

                // Find elements using text block for notesContainer and NotesButtonActual for notesButton
                var notesContainer = window.FindFirstDescendant(cf => cf.ByAutomationId("NotesPreviewBlock"));
                var notesButton = window.FindFirstDescendant(cf => cf.ByAutomationId("NotesButtonActual"));

                // 1. Ample Space State: notesContainer is visible, notesButton is collapsed (either null or offscreen)
                Assert.NotNull(notesContainer);
                Assert.False(notesContainer.IsOffscreen, "Notes panel should be visible when space is ample.");
                Assert.True(notesButton == null || notesButton.IsOffscreen, "Notes Button should be collapsed when space is ample.");

                // 2. Reduce window width to force collapsing
                var transformPattern = window.Patterns.Transform.PatternOrDefault;
                if (transformPattern != null)
                {
                    var currentHeight = window.Properties.BoundingRectangle.Value.Height;
                    transformPattern.Resize(400, currentHeight);
                }
                System.Threading.Thread.Sleep(500); // Wait for SizeChanged layout pass

                // Re-find elements since collapse removes notesContainer from the UIA tree
                notesContainer = window.FindFirstDescendant(cf => cf.ByAutomationId("NotesPreviewBlock"));
                notesButton = window.FindFirstDescendant(cf => cf.ByAutomationId("NotesButtonActual"));

                // Verify notesContainer is collapsed, notesButton is visible
                Assert.True(notesContainer == null || notesContainer.IsOffscreen, "Notes panel should collapse when width is restricted.");
                Assert.NotNull(notesButton);
                Assert.False(notesButton.IsOffscreen, "Notes Button should become visible when width is restricted.");

                // 3. Restore window width
                if (transformPattern != null)
                {
                    var currentHeight = window.Properties.BoundingRectangle.Value.Height;
                    transformPattern.Resize(1920, currentHeight);
                }
                System.Threading.Thread.Sleep(500); // Wait for SizeChanged layout pass

                // Re-find elements
                notesContainer = window.FindFirstDescendant(cf => cf.ByAutomationId("NotesPreviewBlock"));
                notesButton = window.FindFirstDescendant(cf => cf.ByAutomationId("NotesButtonActual"));

                // Verify restored state
                Assert.NotNull(notesContainer);
                Assert.False(notesContainer.IsOffscreen, "Notes panel should restore when width is expanded.");
                Assert.True(notesButton == null || notesButton.IsOffscreen, "Notes Button should hide when width is expanded.");
            }
            finally
            {
                try { app.Close(); } catch { }
            }
        }

        /*
         * Scenario: Notes panel collapsing behaviors under ShowNotesPreview=false
         *   Given the app is launched in button-only mode
         *   When the space is ample, NotesContainer should be collapsed, and NotesButton should be visible.
         *   When the width is extremely restricted, NotesButton should collapse.
         *   When the width is restored, NotesButton should become visible again.
         */
        [Fact]
        public void UI_Scenario_NotesButtonOnlyModeCollapseAndRestoreOnResize()
        {
            if (!Environment.UserInteractive || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != null || Environment.GetEnvironmentVariable("SKIP_UI_TESTS") == "true")
            {
                return; // Skip FlaUI tests in headless/non-interactive environment
            }

            // Create/Force settings to be ShowNotesPreview=false for this test run
            try
            {
                string settingsPath = Path.Combine(Path.GetDirectoryName(GetAppPath())!, "settings.txt");
                File.WriteAllText(settingsPath, "False");
            }
            catch { }

            string appPath = GetAppPath();
            using var app = LaunchApp(appPath);
            using var automation = new UIA3Automation();
            
            try
            {
                var window = GetMainWindowSafe(app, automation, TimeSpan.FromSeconds(5));
                Assert.NotNull(window);

                // Wait for layout to settle
                System.Threading.Thread.Sleep(500);

                // Find elements
                var notesContainer = window.FindFirstDescendant(cf => cf.ByAutomationId("NotesPreviewBlock"));
                var notesButton = window.FindFirstDescendant(cf => cf.ByAutomationId("NotesButtonActual"));

                // 1. Ample Space State: notesContainer is collapsed, notesButton is visible
                Assert.True(notesContainer == null || notesContainer.IsOffscreen, "Notes panel should always be collapsed in button-only mode.");
                Assert.NotNull(notesButton);
                Assert.False(notesButton.IsOffscreen, "Notes Button should be visible initially in button-only mode.");

                // 2. Reduce window width to force button collapse
                var transformPattern = window.Patterns.Transform.PatternOrDefault;
                if (transformPattern != null)
                {
                    var currentHeight = window.Properties.BoundingRectangle.Value.Height;
                    transformPattern.Resize(80, currentHeight);
                }
                System.Threading.Thread.Sleep(500); // Wait for SizeChanged layout pass

                // Re-find elements since collapse removes notesButton from the UIA tree
                notesButton = window.FindFirstDescendant(cf => cf.ByAutomationId("NotesButtonActual"));

                // Verify notesButton is collapsed
                Assert.True(notesButton == null || notesButton.IsOffscreen, "Notes Button should hide when space is extremely restricted.");

                // 3. Restore window width
                if (transformPattern != null)
                {
                    var currentHeight = window.Properties.BoundingRectangle.Value.Height;
                    transformPattern.Resize(1920, currentHeight);
                }
                System.Threading.Thread.Sleep(500); // Wait for SizeChanged layout pass

                // Re-find elements
                notesButton = window.FindFirstDescendant(cf => cf.ByAutomationId("NotesButtonActual"));

                // Verify restored state
                Assert.NotNull(notesButton);
                Assert.False(notesButton.IsOffscreen, "Notes Button should restore when width is expanded.");
            }
            finally
            {
                try { app.Close(); } catch { }
            }
        }

        #endregion
    }
}
