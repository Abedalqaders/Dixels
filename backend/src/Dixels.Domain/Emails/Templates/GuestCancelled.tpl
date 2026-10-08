{{~ if model.cancelled_by_admin ~}}
<p style="margin:12px 0 0;border-radius:8px;padding:10px 12px;font-size:14px;background:#fef2f2;color:#b91c1c;">{{ L "Email:GuestCancelled:ByAdmin" }}{{ if model.reason }}<br><b>{{ L "Email:Reason" }}:</b> {{ model.reason | html.escape }}{{ end }}</p>
{{~ else if model.removed_by_owner ~}}
<p style="margin:12px 0 0;border-radius:8px;padding:10px 12px;font-size:14px;background:#f4f4f5;color:#52525b;">{{ L "Email:GuestRemoved:Note" }}</p>
{{~ else ~}}
<p style="margin:12px 0 0;border-radius:8px;padding:10px 12px;font-size:14px;background:#f4f4f5;color:#52525b;">{{ L "Email:GuestCancelled:ByOwner" (model.invited_by | html.escape) }}</p>
{{~ end ~}}
