param($TaskName = "작업 완료")

# 커스텀 앱 ID를 레지스트리에 등록 (최초 1회)
$appId = 'AfterYou.Harness'
$regPath = "HKCU:\SOFTWARE\Classes\AppUserModelId\$appId"
if (-not (Test-Path $regPath)) {
    New-Item -Path $regPath -Force | Out-Null
    New-ItemProperty -Path $regPath -Name 'DisplayName' -Value 'AfterYou' -PropertyType String -Force | Out-Null
}

[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType=WindowsRuntime] | Out-Null
[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType=WindowsRuntime] | Out-Null

$toastXml = @"
<toast launch="vscode://file/D:/AfterYou" activationType="protocol">
  <audio src="ms-winsoundevent:Notification.Default"/>
  <visual>
    <binding template="ToastGeneric">
      <text>AfterYou — 하네스 완료</text>
      <text>$TaskName</text>
    </binding>
  </visual>
</toast>
"@

$xml = New-Object Windows.Data.Xml.Dom.XmlDocument
$xml.LoadXml($toastXml)
$toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($appId).Show($toast)
