const appointmentProjections = {
    // These are display copies only. Never pass a rendering ID to an appointment endpoint.
    project(events, visibleResources) {
        return events.flatMap(event => {
            const primary = {
                ...event,
                eventKind: "appointment",
                projectionKind: "primary",
                resourceUid: event.resource
            };
            const entries = [primary];
            if (event.roomResourceUid && event.roomResourceUid !== event.resource) {
                entries.push({
                    ...primary,
                    id: `${event.appointmentUid}:room`,
                    resource: event.roomResourceUid,
                    resourceUid: event.roomResourceUid,
                    projectionKind: "room",
                    moveDisabled: true,
                    resizeDisabled: true
                });
            }
            return entries.filter(entry => visibleResources.has(entry.resource));
        });
    },
    appointmentUid(event) {
        return event?.eventKind === "appointment" && event.appointmentUid
            ? event.appointmentUid : null;
    },
    canMove(event, canManage) {
        return canManage && event?.projectionKind === "primary" && !event.moveDisabled
            && this.appointmentUid(event) !== null;
    }
};
window.ScheduleAppointmentProjections = appointmentProjections;
export {};
//# sourceMappingURL=appointment-projections.js.map