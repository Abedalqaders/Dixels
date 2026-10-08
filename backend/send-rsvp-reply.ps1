# Pretends to be a guest's mail app answering a Dixels invite (E6): sends an iTIP REPLY
# (Accept / Decline) to rsvp@ through this worktree's smtp4dev, where the running API's
# mailbox reader picks it up within a minute. Outlook and Gmail can't reach smtp4dev, so this
# is how to try it in dev. Reads the ports from backend/.env, like run-local.ps1.
#
#   .\send-rsvp-reply.ps1 -To rana@test.io -Answer Accepted
#       answers the newest invite smtp4dev caught for rana@test.io (its UID is in invite.ics)
#   .\send-rsvp-reply.ps1 -To rana@test.io -Answer Declined -Date 2026-10-20T09:00
#       a series: declines that one date only (its start, building time)
#   .\send-rsvp-reply.ps1 -IcsUid 'Xp3…Q@dixels' -Answer Accepted
#       answers a given invite UID
param(
    [string]$To,
    [string]$IcsUid,
    [Parameter(Mandatory)][ValidateSet('Accepted', 'Declined', 'Tentative')][string]$Answer,
    [string]$Date
)
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
foreach ($line in Get-Content (Join-Path $root '.env')) {
    if ($line -match '^\s*(DIXELS_MAIL_PORT)\s*=\s*(.*?)\s*$') {
        [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2], 'Process')
    }
}
$mailPort = if ($env:DIXELS_MAIL_PORT) { $env:DIXELS_MAIL_PORT } else { '5000' }
$smtpPort = [int]$mailPort + 20000
$inbox = "http://localhost:$mailPort"

if (-not $IcsUid) {
    if (-not $To) { throw 'Give -To (the guest whose newest invite to answer) or -IcsUid.' }

    # The newest invite to them: its calendar file carries their own secret UID.
    function Find-Ics($part) {
        foreach ($a in $part.attachments) { if ($a.fileName -eq 'invite.ics') { return $a.url } }
        foreach ($child in $part.childParts) { $url = Find-Ics $child; if ($url) { return $url } }
        return $null
    }
    $messages = (Invoke-RestMethod "$inbox/api/messages?pageSize=200").results |
        Where-Object { $_.to -contains $To } | Sort-Object receivedDate -Descending
    foreach ($m in $messages) {
        $url = Find-Ics (Invoke-RestMethod "$inbox/api/messages/$($m.id)").parts[0]
        if (-not $url) { continue }
        $ics = (Invoke-WebRequest -UseBasicParsing "$inbox/$url").Content
        if ($ics -is [byte[]]) { $ics = [Text.Encoding]::UTF8.GetString($ics) }
        $ics = $ics -replace "`r`n ", ''
        if ($ics -match 'METHOD:(REQUEST|PUBLISH)' -and $ics -match 'UID:(\S+@dixels)') {
            $IcsUid = $Matches[1]
            Write-Host "Answering '$($m.subject)' ($IcsUid)"
            break
        }
    }
    if (-not $IcsUid) { throw "No invite to $To found in smtp4dev ($inbox)." }
}

$partstat = $Answer.ToUpperInvariant()
$stamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'")
$recurrence = if ($Date) { "RECURRENCE-ID:" + ([datetime]$Date).ToString("yyyyMMdd'T'HHmmss") + "`r`n" } else { '' }
$guest = if ($To) { $To } else { 'guest@example.com' }
$calendar = "BEGIN:VCALENDAR`r`nMETHOD:REPLY`r`nPRODID:-//Dixels//send-rsvp-reply//EN`r`nVERSION:2.0`r`n" +
    "BEGIN:VEVENT`r`nUID:$IcsUid`r`n$recurrence" + "DTSTAMP:$stamp`r`n" +
    "ATTENDEE;PARTSTAT=${partstat}:mailto:$guest`r`nEND:VEVENT`r`nEND:VCALENDAR`r`n"

$mail = New-Object System.Net.Mail.MailMessage
$mail.From = New-Object System.Net.Mail.MailAddress($guest)
$mail.To.Add('rsvp@dixels.local')
$mail.Subject = "${Answer}: (test reply)"
$mail.Body = "$guest has $($Answer.ToLowerInvariant()) (sent by send-rsvp-reply.ps1)."
$type = New-Object System.Net.Mime.ContentType('text/calendar')
$type.CharSet = 'utf-8'
$type.Parameters.Add('method', 'REPLY')
$mail.AlternateViews.Add([System.Net.Mail.AlternateView]::CreateAlternateViewFromString($calendar, $type))

$smtp = New-Object System.Net.Mail.SmtpClient('localhost', $smtpPort)
try { $smtp.Send($mail) } finally { $smtp.Dispose(); $mail.Dispose() }
Write-Host "Sent $Answer to rsvp@ via smtp4dev (SMTP localhost:$smtpPort). The API applies it within a minute."
