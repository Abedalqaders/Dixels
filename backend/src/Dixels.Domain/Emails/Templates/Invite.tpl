<div style="border-top:2px dashed #e4e4e7;margin:4px 0 12px;"></div>
<table role="presentation" style="width:100%;border-collapse:collapse;font-size:14px;color:#3f3f46;">
{{~ if model.is_series ~}}
  <tr><td style="padding:5px 0;color:#71717a;width:36%;vertical-align:top;">{{ L "Email:Repeats" }}</td><td style="padding:5px 0;">{{ model.repeats_from }}</td></tr>
{{~ end ~}}
{{~ if model.not_on ~}}
  <tr><td style="padding:5px 0;color:#71717a;width:36%;vertical-align:top;">{{ L "Email:NotOn" }}</td><td style="padding:5px 0;">{{ model.not_on }}</td></tr>
{{~ end ~}}
{{~ if model.address ~}}
  <tr><td style="padding:5px 0;color:#71717a;width:36%;vertical-align:top;">{{ L "Email:Address" }}</td><td style="padding:5px 0;">{{ model.address | html.escape }}</td></tr>
{{~ end ~}}
  <tr><td style="padding:5px 0;color:#71717a;width:36%;vertical-align:top;">{{ L "Email:InvitedBy" }}</td><td style="padding:5px 0;">{{ model.invited_by | html.escape }}</td></tr>
{{~ if model.also_invited ~}}
  <tr><td style="padding:5px 0;color:#71717a;width:36%;vertical-align:top;">{{ L "Email:AlsoInvited" }}</td><td style="padding:5px 0;">{{ model.also_invited | html.escape }}</td></tr>
{{~ end ~}}
</table>
{{~ if model.can_open ~}}
<table role="presentation" style="margin-top:20px;border-collapse:collapse;"><tr><td><a href="{{ model.view_url | html.escape }}" style="display:inline-block;background:#713c91;color:#ffffff;text-decoration:none;padding:10px 18px;border-radius:8px;font-weight:600;font-size:14px;">{{ L "Email:OpenInDixels" }}</a></td></tr></table>
{{~ end ~}}
