{{~ if model.rows.size == 1 && model.reason ~}}
<p style="margin:12px 0 0;border-radius:8px;padding:10px 12px;font-size:14px;background:#fef2f2;color:#b91c1c;"><b>{{ L "Email:Reason" }}:</b> {{ model.reason | html.escape }}</p>
{{~ end ~}}
{{~ if model.more > 0 ~}}
<p style="margin:12px 0 0;border-radius:8px;padding:10px 12px;font-size:14px;background:#f4f4f5;color:#52525b;">{{ L "Email:AdminCancelled:More" model.more }}</p>
{{~ end ~}}
{{~ if model.guests_told > 0 ~}}
<p style="margin:12px 0 0;border-radius:8px;padding:10px 12px;font-size:14px;background:#f4f4f5;color:#52525b;">{{ if model.guests_told == 1 }}{{ L "Email:GuestsTold:One" }}{{ else }}{{ L "Email:GuestsTold" model.guests_told }}{{ end }}</p>
{{~ end ~}}
<table role="presentation" style="margin-top:20px;border-collapse:collapse;"><tr><td><a href="{{ model.find_url | html.escape }}" style="display:inline-block;background:#713c91;color:#ffffff;text-decoration:none;padding:10px 18px;border-radius:8px;font-weight:600;font-size:14px;">{{ L "Email:FindAnotherRoom" }}</a></td></tr></table>
