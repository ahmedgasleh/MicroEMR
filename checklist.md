[*] Web app redirects to Auth login when not logged in
[*] Login works
[*] User returns to MicroEMR.Web after login
[ ] Logout works
[ ] Logout returns to login screen
[ ] No token/authority errors
[*] Dashboard opens after login
[*] Footer spacing/gap issue is still fixed
[*] Book Appointment button works
[*] Book Appointment opens Scheduling/Add Appointment workflow
[*] Today's Schedule loads today's active appointments
[*] Cancelled appointments do not show in Today's Schedule
[*] Open Chart button works
[*] View Schedule button works
[*] Status dropdown appears for Today's Schedule appointments
[*] Scheduled → Arrived works
[*] Arrived → Roomed works
[*] Roomed → Seen works
[*] Seen → Completed works
[*] Status persists after page refresh
[ ] Invalid status is rejected
[ ] Cancelled appointment cannot be updated
[*] No JavaScript console errors

[*] Patient search page opens
[*] Existing patient search works
[*] Open Chart works
[*] Register Patient works
[*] New patient appears/searches after registration

[*] Patient Details opens
[*] Demographics tab loads
[*] Edit Demographics works
[*] Documents tab loads
[*] New Document works
[*] Open Document works
[*] Encounters tab loads
[*] Allergies tab loads
[ ] Add/Edit Allergy works if implemented
[*] Medications tab loads
[ ] Add/Edit Medication works if implemented

[*] Scheduling page opens
[*] Resources load as columns
[*] Appointments load for selected day
[*] Previous Day works
[*] Today works
[*] Next Day works
[*] Resource filter works if currently implemented
[*] No JavaScript console errors

[*] Click empty calendar slot opens Add Appointment modal
[*] Date/time/resource prefill correctly
[*] Patient search works
[*] Patient search result dropdown does not expand modal
[*] Selecting patient fills hidden PatientUid
[*] Save Appointment works
[*] New appointment appears on calendar
[*] New appointment appears on Dashboard Today's Schedule if for today

[*] Click existing appointment opens Appointment Details modal
[*] Details show correct patient
[*] Details show correct date/time
[*] Details show provider/resource
[*] Open Chart from details works

[ ] Edit appointment opens from details modal
[ ] Change time works
[ ] Change provider/resource works
[ ] Change room works if implemented
[ ] Change type/reason/notes works
[ ] Save closes modal or updates correctly
[ ] Calendar refreshes
[ ] Dashboard reflects updated time/status

[*] Drag appointment to another slot same provider works
[*] Drag appointment to another provider works
[*] Confirmation modal appears
[*] Confirm Move saves
[*] Cancel/Keep Current Time restores old position
[*] Calendar refreshes after move
[*] Dashboard reflects updated time

[*] Cancel Appointment button exists in Appointment Details modal
[*] Cancel confirmation opens
[*] Cancel reason can be entered
[*] Confirm Cancel marks AppointmentStatus = Cancelled
[*] Cancelled appointment disappears from Scheduling calendar
[*] Cancelled appointment disappears from Dashboard Today's Schedule
[*] SQL shows Cancelled status
[*] Existing add/edit/drag/drop still work after cancel restore