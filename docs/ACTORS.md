# Actor System Overview

This document describes the actor-based architecture used to manage vessels and ports in the CQRS-vessels system. The system uses three main types of actors that work together to coordinate complex operations while maintaining consistency and reliability.

## What are Actors?

Actors are independent, autonomous entities that:
- Maintain their own state
- Process messages one at a time (no race conditions)
- Communicate with other actors through messages
- Can recover from failures and restore their state

## The Three Main Actors

### VesselActor

**Purpose:** Each VesselActor represents and manages the lifecycle of a single vessel.

**Responsibilities:**
- Track vessel registration and identity
- Maintain current position and location history
- Handle arrival at ports
- Manage departure from ports
- Track operational status (active, maintenance, decommissioned)
- Persist all vessel events for complete audit trail

**Key Behaviors:**
- Vessels can move freely between positions
- Must follow proper arrival procedures when approaching a port
- Cannot depart unless currently docked
- Maintains historical record of all movements and status changes

---

### PortActor

**Purpose:** Each PortActor represents and manages a single port facility.

**Responsibilities:**
- Manage port registration and capacity
- Handle docking reservation requests from vessels
- Confirm vessel dockings
- Track currently docked vessels
- Manage port operational status (open/closed)
- Automatically expire stale reservations

**Key Behaviors:**
- Accepts reservation requests when capacity is available
- Enforces capacity limits (rejects requests when full)
- Runs periodic checks to expire old reservations (every minute)
- Maintains accurate state of all dock positions
- Ensures vessels have valid reservations before docking

**Automated Background Tasks:**
- Every minute, scans for expired reservations and automatically expires them
- This prevents "zombie reservations" from blocking dock space

---

### DockingSaga

**Purpose:** Orchestrates the complex, multi-step docking process between a vessel and a port. Acts as a coordinator to ensure both parties complete their parts of the transaction.

**Why is a Saga needed?**

Docking is a distributed transaction involving two independent actors (vessel and port). If any step fails, we need to ensure cleanup happens properly. The saga pattern ensures:
1. All steps complete successfully, OR
2. Failed operations are properly compensated (cleaned up)

**The Docking Process:**

The saga coordinates three main steps:

**Step 1: Reserve Dock Space**
- Saga requests a docking reservation from the port
- Port checks capacity and creates a reservation
- If port is full or closed → saga fails and notifies caller
- If successful → saga proceeds to step 2

**Step 2: Vessel Arrival**
- Saga commands the vessel to arrive at the port
- Vessel verifies it can arrive and records the arrival
- If vessel cannot arrive → saga compensates by expiring the port reservation
- If successful → saga proceeds to step 3

**Step 3: Confirm Docking**
- Saga asks the port to confirm the docking
- Port validates the reservation and confirms the docking
- If confirmation fails → saga compensates by expiring the reservation
- If successful → saga completes successfully

**Compensation (Cleanup on Failure):**

If any step fails, the saga automatically:
- Expires any reservations that were created
- Ensures the port doesn't have "stuck" reservations
- Notifies the original caller of the failure

**Timeout Handling:**
- If a reservation request times out, the saga automatically compensates
- Prevents resources from being locked indefinitely

---

## How They Work Together

### Communication Pattern

Actors communicate using the **Ask pattern**:
- One actor sends a command to another
- The sender waits for a response
- The receiver processes the command and replies with success or failure
- The sender can then decide what to do next based on the response

This is different from "fire and forget" messaging - the saga needs to know if each step succeeded before proceeding.

### Actor Lifecycle

**VesselActor and PortActor:**
- Created on-demand when first referenced
- Recover their complete state from event history when starting
- Run indefinitely while the system is active
- One actor instance per vessel/port

**DockingSaga:**
- Created for each docking operation
- Runs only for the duration of the docking process
- Automatically stops when completed (successfully or failed)
- Short-lived and task-focused

### Event Sourcing

Both VesselActor and PortActor use **Event Sourcing**:
- Every state change is recorded as an event
- Events are persisted to the database
- State is rebuilt by replaying all events
- Provides complete audit trail
- Enables recovery after crashes

DockingSaga is **in-memory only**:
- Does not persist events
- If the saga crashes mid-process, reservations will eventually expire
- Designed to be short-lived (seconds to minutes)

---

## Docking Flow Example

Here's what happens when a vessel wants to dock:

1. **Client/API** sends a "StartDocking" command with vessel ID and port ID

2. **DockingSaga is created** for this specific docking operation

3. **Saga → Port:** "Please reserve a dock for this vessel"
   - Port checks capacity
   - Port creates reservation with expiration time
   - Port replies "Reservation successful" with reservation ID

4. **Saga → Vessel:** "Please arrive at this port with this reservation"
   - Vessel validates it can arrive
   - Vessel records arrival event
   - Vessel replies "Arrival successful"

5. **Saga → Port:** "Please confirm docking for this reservation"
   - Port validates the reservation exists
   - Port confirms the docking
   - Port replies "Docking confirmed"

6. **Saga → Client:** "Docking completed successfully"

7. **Saga stops** (job complete)

### What if something goes wrong?

**Scenario: Port is full**
- Step 3 fails with "Port at capacity"
- Saga immediately notifies client of failure
- No compensation needed (nothing was reserved)

**Scenario: Vessel cannot arrive**
- Step 4 fails with "Vessel not at port location"
- Saga sends "ExpireReservation" to port
- Port frees up the reserved dock space
- Saga notifies client of failure

**Scenario: Process takes too long**
- Port's automatic expiration (background task) cleans up the reservation
- Prevents indefinite resource locking
- Saga's timeout handling triggers compensation if still running

---

## Key Design Decisions

### Why typed actors?

Each actor has a specific protocol defining exactly what messages it accepts. This provides:
- Compile-time safety (can't send wrong message types)
- Clear contracts between actors
- Better code maintainability

### Why Ask instead of Tell?

The saga uses Ask (request-response) instead of Tell (fire-and-forget) because:
- Saga needs to know if each step succeeded
- Responses determine the next step
- Enables proper error handling and compensation
- Keeps the saga's mailbox clean (responses come back as async results)

### Why automatic reservation expiration?

Ports periodically check for expired reservations because:
- Prevents resources from being locked forever
- Handles cases where sagas crash or timeout
- Ensures eventual consistency
- No manual intervention needed

---

## Summary

The actor system provides a robust, scalable way to manage vessel and port operations:

- **VesselActor** and **PortActor** maintain domain state with full event history
- **DockingSaga** coordinates complex operations across multiple actors
- Actors are isolated, eliminating race conditions
- Saga pattern ensures consistency even when operations fail
- Automatic cleanup prevents resource leaks
- Event sourcing provides complete auditability

This architecture allows the system to handle concurrent operations safely while maintaining data consistency and providing clear business-level coordination.
