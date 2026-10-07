<p style="margin:0 0 8px;font-size:16px;">{{ L "Email:Greeting" (model.recipient_name | html.escape) }}</p>
<h1 style="margin:0 0 20px;font-size:20px;">{{ if model.count > 1 }}{{ L "Email:AdminCancelled:IntroMany" model.count }}{{ else }}{{ L "Email:AdminCancelled:Intro" }}{{ end }}</h1>
{{~ for row in model.rows ~}}
<table role="presentation" style="width:100%;border-collapse:collapse;font-size:15px;{{ if !for.first }}margin-top:12px;border-top:1px solid #e4e4e7;{{ end }}">
  <tr><td style="padding:6px 0;color:#71717a;width:35%;">{{ L "Email:Space" }}</td><td style="padding:6px 0;font-weight:600;">{{ row.space_name | html.escape }}</td></tr>
  <tr><td style="padding:6px 0;color:#71717a;">{{ L "Email:Where" }}</td><td style="padding:6px 0;">{{ row.floor_name | html.escape }} · {{ row.building_name | html.escape }}</td></tr>
  <tr><td style="padding:6px 0;color:#71717a;">{{ L "Email:When" }}</td><td style="padding:6px 0;text-decoration:line-through;">{{ row.date }}</td></tr>
  <tr><td style="padding:6px 0;color:#71717a;">{{ L "Email:Time" }}</td><td style="padding:6px 0;"><span dir="ltr">{{ row.time }}</span></td></tr>
{{~ if row.title ~}}
  <tr><td style="padding:6px 0;color:#71717a;">{{ L "Email:Title" }}</td><td style="padding:6px 0;">{{ row.title | html.escape }}</td></tr>
{{~ end ~}}
{{~ if row.reason ~}}
  <tr><td style="padding:6px 0;color:#71717a;">{{ L "Email:Reason" }}</td><td style="padding:6px 0;">{{ row.reason | html.escape }}</td></tr>
{{~ end ~}}
</table>
{{~ end ~}}
{{~ if model.more > 0 ~}}
<p style="margin:16px 0 0;color:#71717a;">{{ L "Email:AdminCancelled:More" model.more }}</p>
{{~ end ~}}
<p style="margin:24px 0 0;"><a href="{{ model.app_url }}" style="display:inline-block;background:#18181b;color:#ffffff;text-decoration:none;padding:10px 18px;border-radius:8px;font-weight:600;">{{ L "Email:OpenDixels" }}</a></p>
