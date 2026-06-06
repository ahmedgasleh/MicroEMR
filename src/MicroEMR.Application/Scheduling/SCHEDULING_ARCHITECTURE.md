# MicroEMR Scheduling Module Architecture

## Overview
The scheduling module provides comprehensive appointment and calendar management with support for:
- **15-minute slot intervals** for precise scheduling
- **Multiple providers** with individual calendars
- **Multiple resources/rooms** with resource booking
- **Resource blocking** for breaks, training, and maintenance
- **Appointment management** (create, reschedule, cancel)
- **Audit history** of all scheduling changes
- **Patient-focused availability** search

## Architecture Layers

### Domain Layer (MicroEMR.Core)
Defines core domain interfaces:

- **IAppointment**: Patient appointment contract
- **IScheduleSlot**: 15-minute calendar slot
- **IResourceBlock**: Provider/resource availability blocks
- **IAppointmentHistory**: Audit trail of appointment changes

### Application Layer (MicroEMR.Application)
Business logic and service interfaces:

#### Services
- **IAppointmentService**: Create, reschedule, cancel appointments with audit logging
- **IScheduleSlotService**: Generate and manage 15-minute slots
- **IResourceBlockService**: Manage provider/resource blocks
- **ICalendarService**: Multi-provider/resource calendar views

#### DTOs
- **AppointmentDto**: Appointment data transfer object
- **ScheduleSlotDto**: Schedule slot representation
- **ResourceBlockDto**: Resource block representation
- **CalendarViewDto**: Complete calendar view with slots, appointments, and blocks

### API Layer (MicroEMR.Api)
RESTful endpoints for system integration:

- `POST /api/appointments` - Create appointment
- `GET /api/appointments/{id}` - Get appointment details
- `PUT /api/appointments/{id}/reschedule` - Reschedule appointment
- `POST /api/appointments/{id}/cancel` - Cancel appointment
- `POST /api/appointments/{id}/confirm` - Confirm appointment
- `GET /api/appointments/patient/{patientId}` - Get patient appointments
- `GET /api/scheduleslots/available` - Get available slots
- `POST /api/scheduleslots/generate` - Generate 15-minute slots
- `GET /api/calendar/provider/{providerId}` - Get provider calendar
- `POST /api/calendar/find-available-slots` - Find slots across providers

### Web Layer (MicroEMR.Web)
User interface for scheduling management:

#### Controllers
- **SchedulingController**: Manages scheduling views and user interactions

#### Views
- **Index.cshtml**: Main scheduling dashboard
- **Calendar.cshtml**: Provider calendar with drag-and-drop support
- **AppointmentHistory.cshtml**: Audit trail view

## Key Features

### 15-Minute Slot System
```csharp
// Generate 15-minute slots between 08:00 and 17:00
var slots = SchedulingHelper.GenerateSlots(
    new DateTime(2026, 6, 4, 8, 0, 0),
    new DateTime(2026, 6, 4, 17, 0, 0)
);
// Returns slots at 08:00, 08:15, 08:30, etc.
```

### Multi-Provider Calendar
Providers can manage their own schedules independently with support for:
- Multiple resources (exam rooms, equipment)
- Overlapping availability
- Cross-provider scheduling

### Resource Blocking
Block time for:
- **Break**: Lunch and coffee breaks
- **Lunch**: Extended meal periods
- **Training**: Professional development
- **Maintenance**: Equipment servicing

### Audit History
Every appointment change is logged with:
- Change type (Created, Rescheduled, Cancelled, Confirmed)
- Previous and new times
- User who made the change
- Reason for change
- Timestamp

## Data Flow

### Creating an Appointment
1. User selects provider, resource, and available slot
2. System validates no conflicts exist
3. Appointment is created with "Scheduled" status
4. Audit log entry created
5. Confirmation email sent to patient
6. Calendar view updates

### Rescheduling an Appointment
1. User selects new time from available slots
2. System validates new slot is available
3. Old appointment marked with audit entry
4. New appointment created
5. Audit log shows old and new times
6. Patient notified of change

### Cancelling an Appointment
1. User provides cancellation reason
2. System checks if within cancellation window
3. Appointment marked with soft delete
4. Audit log entry created
5. Slot becomes available again
6. Patient notified

## Frontend Interactions

### Calendar Grid View
- Displays 15-minute slots for provider
- Shows color-coded status:
  - Green: Available
  - Blue: Booked (with patient name)
  - Red: Resource block
  - Yellow: Slot blocked

### Drag-and-Drop Rescheduling
Users can drag appointments to new time slots to instantly reschedule with automatic conflict detection.

### Quick Actions
- New Appointment modal
- Resource Block creation
- Generate recurring slots
- View appointment history

## Database Considerations

When implementing the Infrastructure layer:

### ScheduleSlots Table
```sql
CREATE TABLE ScheduleSlots (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    ProviderId UNIQUEIDENTIFIER NOT NULL,
    ClinicResourceId UNIQUEIDENTIFIER,
    SlotStartTime DATETIME2 NOT NULL,
    SlotEndTime DATETIME2 NOT NULL,
    Status NVARCHAR(50) NOT NULL, -- Available, Blocked, Booked
    BlockReason NVARCHAR(MAX),
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2,
    IsDeleted BIT NOT NULL DEFAULT 0
);

CREATE INDEX idx_provider_date ON ScheduleSlots(ProviderId, SlotStartTime);
CREATE INDEX idx_resource_date ON ScheduleSlots(ClinicResourceId, SlotStartTime);
```

### ResourceBlocks Table
```sql
CREATE TABLE ResourceBlocks (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    ResourceId UNIQUEIDENTIFIER NOT NULL,
    ProviderId UNIQUEIDENTIFIER NOT NULL,
    BlockStartTime DATETIME2 NOT NULL,
    BlockEndTime DATETIME2 NOT NULL,
    Reason NVARCHAR(MAX) NOT NULL,
    BlockType NVARCHAR(50), -- Break, Lunch, Training, Maintenance
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedBy UNIQUEIDENTIFIER NOT NULL
);

CREATE INDEX idx_provider_date ON ResourceBlocks(ProviderId, BlockStartTime);
```

### AppointmentHistory Table
```sql
CREATE TABLE AppointmentHistory (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    AppointmentId UNIQUEIDENTIFIER NOT NULL,
    ChangedAt DATETIME2 NOT NULL,
    ChangedBy UNIQUEIDENTIFIER NOT NULL,
    ChangeType NVARCHAR(50), -- Created, Rescheduled, Cancelled, Confirmed
    OldStartTime NVARCHAR(MAX),
    NewStartTime NVARCHAR(MAX),
    Reason NVARCHAR(MAX)
);

CREATE INDEX idx_appointment_date ON AppointmentHistory(AppointmentId, ChangedAt);
```

## Security Considerations

- Role-based access control (RBAC) for scheduling operations
- Audit logging for compliance
- Soft deletes for HIPAA compliance (never physically delete patient data)
- User identification for all changes
- OAuth2/OpenID Connect integration ready

## Performance Optimization

- Index schedule slots by provider and date
- Cache provider availability for 15-minute lookups
- Batch generate slots for weekly patterns
- Query appointment history by date range
- Implement pagination for large result sets

## Future Enhancements

1. **Waitlist Management**: Auto-notifypatients of cancellations
2. **Appointment Reminders**: SMS/email notifications
3. **Provider Preferences**: Availability templates
4. **Patient Preferences**: Preferred providers and times
5. **No-Show Tracking**: Mark appointments as no-show
6. **Recurring Appointments**: Series of appointments
7. **Double-Booking Prevention**: Confirmation gates
8. **Bulk Rescheduling**: Cascade reschedules
