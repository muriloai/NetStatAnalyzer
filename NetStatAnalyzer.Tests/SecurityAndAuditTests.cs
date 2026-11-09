using System.Collections.Generic;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Enums;
using NetStatAnalyzer.Infrastructure.System;
using NetStatAnalyzer.Presentation.ViewModels;
using Xunit;

namespace NetStatAnalyzer.Tests
{
    public class SecurityAndAuditTests
    {
        [Theory]
        [InlineData("\"C:\\Program Files\\Adobe\\Acrobat\\acrotray.exe\" /arg", "C:\\Program Files\\Adobe\\Acrobat\\acrotray.exe")]
        [InlineData("C:\\Windows\\System32\\cmd.exe /c start", "C:\\Windows\\System32\\cmd.exe")]
        [InlineData("\"C:\\Users\\Test\\app.exe\"", "C:\\Users\\Test\\app.exe")]
        [InlineData("notepad.exe", "notepad.exe")]
        public void ExtractExecutablePath_ShouldParseCommandsCorrectly(string command, string expected)
        {
            string actual = FileSecurityInspector.ExtractExecutablePath(command);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void StartupItemViewModel_UnsignedItem_ShouldNotBeConfiavel()
        {
            var item = new StartupItem
            {
                Name = "acrotray",
                Command = @"C:\Program Files\Adobe\acrotray.exe",
                Location = "HKCU Run",
                ThreatLevel = ThreatLevel.Low,
                ThreatReason = "Executável sem assinatura digital válida (Não Homologado)",
                IsSigned = false,
                FileExists = true
            };

            var vm = new StartupItemViewModel(item);

            Assert.NotEqual("CONFIÁVEL", vm.ThreatBadgeText);
            Assert.Contains("NÃO ASSINADO", vm.ThreatBadgeText);
        }

        [Fact]
        public void StartupItemViewModel_OrphanItem_ShouldShowOrphanBadge()
        {
            var item = new StartupItem
            {
                Name = "old_deleted_app",
                Command = @"C:\NonExistent\app.exe",
                Location = "HKLM Run",
                ThreatLevel = ThreatLevel.Medium,
                ThreatReason = "Arquivo inexistente",
                IsSigned = false,
                FileExists = false
            };

            var vm = new StartupItemViewModel(item);

            Assert.NotEqual("CONFIÁVEL", vm.ThreatBadgeText);
            Assert.Contains("ÓRFÃO", vm.ThreatBadgeText);
        }

        [Fact]
        public void ScheduledTaskItemViewModel_ShouldNotBeConfiavel()
        {
            var task = new ScheduledTaskItem
            {
                TaskName = "Updater",
                TaskPath = @"\",
                TaskToRun = @"C:\Users\AppData\updater.exe",
                IsMicrosoftTask = false,
                IsSigned = false,
                FileExists = true,
                ThreatLevel = ThreatLevel.Low
            };

            var vm = new ScheduledTaskItemViewModel(task);

            Assert.NotEqual("CONFIÁVEL", vm.ThreatBadgeText);
            Assert.Contains("NÃO ASSINADO", vm.ThreatBadgeText);
        }

        [Fact]
        public void ConnectionItemViewModel_ShouldHaveExplicitBadgeTextForUnallowed()
        {
            var conn = new NetworkConnection
            {
                PID = 1234,
                ProcessName = "chrome",
                LocalAddress = "192.168.1.10:50000",
                ForeignAddress = "142.250.190.46:443",
                IsTrusted = false,
                ThreatLevel = ThreatLevel.Normal
            };

            var vm = new ConnectionItemViewModel(conn);

            Assert.Equal("Monitorado", vm.AllowBadgeText);
            Assert.Equal(System.Windows.Visibility.Visible, vm.AllowBadgeVisibility);

            conn.IsTrusted = true;
            vm.IsAllowed = true;
            Assert.Equal("🛡️ Confiável", vm.AllowBadgeText);
        }
    }
}
