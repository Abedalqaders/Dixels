<p style="margin:0 0 8px;font-size:16px;">{{ L "Email:Greeting" (model.recipient_name | html.escape) }}</p>
<h1 style="margin:0 0 20px;font-size:20px;">{{ L "Email:BookingReminder:Intro" }}</h1>
<table role="presentation" style="width:100%;border-collapse:collapse;font-size:15px;">
  <tr><td style="padding:6px 0;color:#71717a;width:35%;">{{ L "Email:Space" }}</td><td style="padding:6px 0;font-weight:600;">{{ model.space_name | html.escape }}</td></tr>
  <tr><td style="padding:6px 0;color:#71717a;">{{ L "Email:Where" }}</td><td style="padding:6px 0;">{{ model.floor_name | html.escape }} · {{ model.building_name | html.escape }}</td></tr>
  <tr><td style="padding:6px 0;color:#71717a;">{{ L "Email:When" }}</td><td style="padding:6px 0;">{{ model.date }}</td></tr>
  <tr><td style="padding:6px 0;color:#71717a;">{{ L "Email:Time" }}</td><td style="padding:6px 0;font-weight:600;"><span dir="ltr">{{ model.time }}</span></td></tr>
  <tr><td style="padding:6px 0;color:#71717a;">{{ L "Email:Attendees" }}</td><td style="padding:6px 0;">{{ model.attendees }}</td></tr>
{{~ if model.title ~}}
  <tr><td style="padding:6px 0;color:#71717a;">{{ L "Email:Title" }}</td><td style="padding:6px 0;">{{ model.title | html.escape }}</td></tr>
{{~ end ~}}
</table>
<p style="margin:24px 0 0;"><a href="{{ model.app_url }}" style="display:inline-block;background:#18181b;color:#ffffff;text-decoration:none;padding:10px 18px;border-radius:8px;font-weight:600;">{{ L "Email:OpenDixels" }}</a></p>
