// Scheduling Module JavaScript

// Load providers and resources on page load
document.addEventListener('DOMContentLoaded', function() {
    loadProviders();
    loadResources();
    setupTimeSlots();
});

// Load available providers
async function loadProviders() {
    try {
        const response = await fetch('/api/providers');
        const providers = await response.json();
        
        const selects = ['providerSelect', 'providerSelect2', 'blockProvider', 'slotProvider'];
        selects.forEach(selectId => {
            const select = document.getElementById(selectId);
            if (select) {
                providers.forEach(provider => {
                    const option = document.createElement('option');
                    option.value = provider.id;
                    option.textContent = provider.firstName + ' ' + provider.lastName;
                    select.appendChild(option);
                });
            }
        });
    } catch (error) {
        console.error('Error loading providers:', error);
    }
}

// Load available resources
async function loadResources() {
    try {
        const response = await fetch('/api/clinic-resources');
        const resources = await response.json();
        
        const selects = ['resourceSelect', 'resourceSelect2', 'blockResource', 'slotResource'];
        selects.forEach(selectId => {
            const select = document.getElementById(selectId);
            if (select) {
                resources.forEach(resource => {
                    const option = document.createElement('option');
                    option.value = resource.id;
                    option.textContent = resource.name + ' (' + resource.resourceType + ')';
                    select.appendChild(option);
                });
            }
        });
    } catch (error) {
        console.error('Error loading resources:', error);
    }
}

// View calendar for selected provider
function viewCalendar() {
    const providerId = document.getElementById('providerSelect').value;
    const resourceId = document.getElementById('resourceSelect').value;
    
    if (!providerId) {
        alert('Please select a provider');
        return;
    }
    
    let url = `/scheduling/calendar?providerId=${providerId}`;
    if (resourceId) {
        url += `&clinicResourceId=${resourceId}`;
    }
    
    window.location.href = url;
}

// Setup time slot selectors for schedule generation
function setupTimeSlots() {
    const daysOfWeek = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
    const timeSlots = document.getElementById('timeSlots');
    
    if (timeSlots) {
        daysOfWeek.forEach((day, index) => {
            const dayDiv = document.createElement('div');
            dayDiv.className = 'mb-2';
            dayDiv.innerHTML = `
                <div class="form-check form-switch">
                    <input class="form-check-input" type="checkbox" id="day${index}" 
                           value="${index}" checked />
                    <label class="form-check-label" for="day${index}">
                        <strong>${day}</strong>
                    </label>
                </div>
                <div class="ms-4">
                    <div class="row">
                        <div class="col-md-6">
                            <input type="time" class="form-control form-control-sm" 
                                   id="startTime${index}" value="08:00" />
                        </div>
                        <div class="col-md-6">
                            <input type="time" class="form-control form-control-sm" 
                                   id="endTime${index}" value="17:00" />
                        </div>
                    </div>
                </div>
            `;
            timeSlots.appendChild(dayDiv);
        });
    }
}

// Handle appointment drag and drop
function handleDragStart(e) {
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('appointmentId', e.target.dataset.appointmentId);
}

function handleDragOver(e) {
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';
    e.target.style.backgroundColor = '#e3f2fd';
}

function handleDragLeave(e) {
    e.target.style.backgroundColor = '';
}

function handleDrop(e) {
    e.preventDefault();
    const appointmentId = e.dataTransfer.getData('appointmentId');
    const newSlotTime = e.target.dataset.slotTime;
    
    if (appointmentId && newSlotTime) {
        rescheduleAppointmentByDrag(appointmentId, newSlotTime);
    }
    e.target.style.backgroundColor = '';
}

// Reschedule appointment by dragging
async function rescheduleAppointmentByDrag(appointmentId, newStartTime) {
    try {
        // Calculate end time (30 minutes after start for standard appointment)
        const startDate = new Date(newStartTime);
        const endDate = new Date(startDate.getTime() + 30 * 60000);
        
        const response = await fetch(`/api/appointments/${appointmentId}/reschedule`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                appointmentId: appointmentId,
                newStartAt: startDate.toISOString(),
                newEndAt: endDate.toISOString(),
                reason: 'Rescheduled via drag-and-drop'
            })
        });
        
        if (response.ok) {
            location.reload();
        } else {
            alert('Error rescheduling appointment');
        }
    } catch (error) {
        console.error('Error rescheduling appointment:', error);
        alert('Error rescheduling appointment');
    }
}

// Find available appointment slots
async function findAvailableSlots() {
    const patientId = document.getElementById('patientSelect').value;
    const providerIds = Array.from(document.querySelectorAll('input[name="providers"]:checked'))
        .map(cb => cb.value);
    const preferredDate = document.getElementById('preferredDate').value;
    
    if (!patientId || providerIds.length === 0 || !preferredDate) {
        alert('Please fill in all required fields');
        return;
    }
    
    try {
        const response = await fetch('/api/calendar/find-available-slots', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                patientId: patientId,
                providerIds: providerIds,
                preferredDate: preferredDate,
                durationMinutes: 15
            })
        });
        
        const slots = await response.json();
        displayAvailableSlots(slots);
    } catch (error) {
        console.error('Error finding available slots:', error);
        alert('Error finding available slots');
    }
}

// Display available slots
function displayAvailableSlots(slots) {
    const container = document.getElementById('availableSlotsContainer');
    if (!container) return;
    
    if (slots.length === 0) {
        container.innerHTML = '<p class="alert alert-info">No available slots found</p>';
        return;
    }
    
    let html = '<ul class="list-group">';
    slots.forEach(slot => {
        const startTime = new Date(slot.slotStartTime);
        html += `
            <li class="list-group-item d-flex justify-content-between align-items-center">
                <div>
                    <strong>${slot.providerName}</strong><br>
                    ${startTime.toLocaleDateString()} ${startTime.toLocaleTimeString([], {hour: '2-digit', minute:'2-digit'})}
                </div>
                <button class="btn btn-sm btn-primary" onclick="selectSlot('${slot.id}')">
                    Select
                </button>
            </li>
        `;
    });
    html += '</ul>';
    
    container.innerHTML = html;
}

// Confirm appointment
async function confirmAppointment(appointmentId) {
    try {
        const response = await fetch(`/api/appointments/${appointmentId}/confirm`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        });
        
        if (response.ok) {
            alert('Appointment confirmed');
            location.reload();
        } else {
            alert('Error confirming appointment');
        }
    } catch (error) {
        console.error('Error confirming appointment:', error);
        alert('Error confirming appointment');
    }
}

// Auto-calculate appointment end time (15, 30, or 60 minute duration)
document.addEventListener('change', function(e) {
    if (e.target.id === 'appointmentStart') {
        const startTime = new Date(e.target.value);
        const endTime = new Date(startTime.getTime() + 15 * 60000); // Default 15 minutes
        document.getElementById('appointmentEnd').value = endTime.toISOString().slice(0, 16);
    }
});
