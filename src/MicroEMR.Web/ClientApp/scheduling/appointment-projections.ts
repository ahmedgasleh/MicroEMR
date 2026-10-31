interface AppointmentCalendarEvent {
    id: string;
    appointmentUid: string;
    resource: string;
    roomResourceUid?: string | null;
}

interface AppointmentProjectionData {
    eventKind?: string;
    appointmentUid?: string;
    projectionKind?: string;
    moveDisabled?: boolean;
}

type AppointmentProjection<T> = T & {
    eventKind: "appointment";
    projectionKind: "primary" | "room";
    resourceUid: string;
    moveDisabled?: boolean;
    resizeDisabled?: boolean;
};

const appointmentProjections = {
    // These are display copies only. Never pass a rendering ID to an appointment endpoint.
    project<T extends AppointmentCalendarEvent>(events: readonly T[], visibleResources: ReadonlySet<string>) {
        return events.flatMap(event => {
            const primary: AppointmentProjection<T> = {
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

    appointmentUid(event: AppointmentProjectionData | undefined): string | null {
        return event?.eventKind === "appointment" && event.appointmentUid
            ? event.appointmentUid : null;
    },

    canMove(event: AppointmentProjectionData | undefined, canManage: boolean): boolean {
        return canManage && event?.projectionKind === "primary" && !event.moveDisabled
            && this.appointmentUid(event) !== null;
    }
};

declare global {
    interface Window {
        ScheduleAppointmentProjections: typeof appointmentProjections;
    }
}

window.ScheduleAppointmentProjections = appointmentProjections;
export {};
