{{~ if model.count > 1 || model.reason ~}}
<div style="border-top:2px dashed #e4e4e7;margin:4px 0 12px;"></div>
<table role="presentation" style="width:100%;border-collapse:collapse;font-size:14px;color:#3f3f46;">
{{~ if model.count > 1 ~}}
  <tr><td style="padding:5px 0;color:#71717a;width:36%;vertical-align:top;">{{ L "Email:Dates" }}</td><td style="padding:5px 0;">{{ model.count }}</td></tr>
{{~ end ~}}
{{~ if model.reason ~}}
  <tr><td style="padding:5px 0;color:#71717a;width:36%;vertical-align:top;">{{ L "Email:Reason" }}</td><td style="padding:5px 0;">{{ model.reason | html.escape }}</td></tr>
{{~ end ~}}
</table>
{{~ end ~}}
<table role="presentation" style="margin-top:20px;border-collapse:collapse;"><tr><td><a href="{{ model.find_url | html.escape }}" style="display:inline-block;background:#713c91;color:#ffffff;text-decoration:none;padding:10px 18px;border-radius:8px;font-weight:600;font-size:14px;">{{ L "Email:FindAnotherRoom" }}</a></td></tr></table>
