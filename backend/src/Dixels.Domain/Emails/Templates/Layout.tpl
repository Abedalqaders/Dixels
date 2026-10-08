<!DOCTYPE html>
<html lang="{{ lang }}" dir="{{ dir }}">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
</head>
<body style="margin:0;padding:22px 12px;background:#f4f4f5;font-family:'Segoe UI',Tahoma,Arial,sans-serif;color:#18181b;">
  <div style="max-width:560px;margin:0 auto;background:#713c91;border-radius:12px 12px 0 0;padding:13px 26px;color:#ffffff;">
    <table role="presentation" style="width:100%;border-collapse:collapse;"><tr>
      <td style="font-weight:800;letter-spacing:.14em;font-size:13px;color:#ffffff;">DIXELS</td>
      <td style="text-align:{{ if dir == "rtl" }}left{{ else }}right{{ end }};font-size:13px;font-weight:700;color:#ffffff;">{{ L ("Email:Status:" + model.status) }}</td>
    </tr></table>
  </div>
  <div style="max-width:560px;margin:0 auto;background:#ffffff;border-radius:0 0 12px 12px;padding:22px 26px;">
    <p style="margin:0 0 6px;font-size:15px;">{{ L "Email:Greeting" (model.recipient_name | html.escape) }}</p>
    <h1 style="margin:0 0 16px;font-size:21px;line-height:1.3;">{{ model.heading | html.escape }}</h1>
{{~ for row in model.rows ~}}
    <table role="presentation" style="width:100%;border-collapse:collapse;font-size:15px;{{ if !for.first }}border-top:1px solid #e4e4e7;{{ end }}"><tr>
      <td style="width:50%;padding:{{ if for.first }}0{{ else }}10px{{ end }} 0 12px;vertical-align:top;">
        <div style="font-size:12px;color:#71717a;">{{ L "Email:When" }}</div>
        <div style="font-weight:700;{{ if model.cancelled }}text-decoration:line-through;color:#71717a;{{ end }}">{{ row.date }}</div>
        <div style="{{ if model.cancelled }}text-decoration:line-through;color:#71717a;{{ end }}"><span dir="ltr">{{ row.time }}</span> · <span dir="ltr">{{ row.zone }}</span></div>
      </td>
      <td style="width:50%;padding:{{ if for.first }}0{{ else }}10px{{ end }} 0 12px;vertical-align:top;">
        <div style="font-size:12px;color:#71717a;">{{ L "Email:Where" }}</div>
        <div style="font-weight:700;">{{ row.space_name | html.escape }}</div>
        <div>{{ row.floor_name | html.escape }} · {{ row.building_name | html.escape }}</div>
      </td>
    </tr>
{{~ if model.rows.size > 1 && (row.title || row.reason) ~}}
    <tr><td colspan="2" style="padding:0 0 10px;font-size:14px;color:#3f3f46;">{{ if row.title }}{{ row.title | html.escape }}{{ end }}{{ if row.title && row.reason }} · {{ end }}{{ if row.reason }}<span style="color:#b91c1c;">{{ row.reason | html.escape }}</span>{{ end }}</td></tr>
{{~ end ~}}
    </table>
{{~ end ~}}
{{ content }}
  </div>
  <p style="max-width:560px;margin:14px auto 0;font-size:12px;color:#71717a;text-align:center;">{{ L "Email:Footer" }}</p>
</body>
</html>
