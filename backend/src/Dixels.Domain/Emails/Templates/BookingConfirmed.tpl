<div style="border-top:2px dashed #e4e4e7;margin:4px 0 12px;"></div>
<table role="presentation" style="width:100%;border-collapse:collapse;font-size:14px;color:#3f3f46;">
{{~ if model.address ~}}
  <tr><td style="padding:5px 0;color:#71717a;width:36%;vertical-align:top;">{{ L "Email:Address" }}</td><td style="padding:5px 0;">{{ model.address | html.escape }}</td></tr>
{{~ end ~}}
  <tr><td style="padding:5px 0;color:#71717a;width:36%;vertical-align:top;">{{ L "Email:Attendees" }}</td><td style="padding:5px 0;">{{ model.attendees }}</td></tr>
</table>
<table role="presentation" style="margin-top:20px;border-collapse:collapse;"><tr><td><a href="{{ model.view_url | html.escape }}" style="display:inline-block;background:#713c91;color:#ffffff;text-decoration:none;padding:10px 18px;border-radius:8px;font-weight:600;font-size:14px;">{{ L "Email:ViewBooking" }}</a></td></tr></table>
<p style="margin:20px 0 0;padding-top:12px;border-top:1px solid #e4e4e7;font-size:12.5px;color:#71717a;">{{ L "Email:ChangeOfPlans" }} <a href="{{ model.view_url | html.escape }}" style="color:#18181b;font-weight:600;">{{ L "Email:CancelBooking" }}</a> · <a href="{{ model.find_url | html.escape }}" style="color:#18181b;font-weight:600;">{{ L "Email:FindAnotherRoom" }}</a></p>
