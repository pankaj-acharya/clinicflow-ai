const slots = document.getElementById("slots");
const status = document.getElementById("status");

document.getElementById("load").addEventListener("click", async () => {
  const clinicId = document.getElementById("clinicId").value;
  const clinicianId = document.getElementById("clinicianId").value;
  const url = `/availability?ClinicId=${encodeURIComponent(clinicId)}&ClinicianId=${encodeURIComponent(clinicianId)}&WindowStartUtc=2026-08-11T00:00:00Z&WindowEndUtc=2026-08-12T00:00:00Z&AppointmentTypeCode=exam`;

  status.textContent = "Loading availability...";
  slots.innerHTML = "";

  try {
    const response = await fetch(url);
    if (!response.ok) {
      throw new Error(`Availability request failed with status ${response.status}.`);
    }

    const data = await response.json();
    slots.innerHTML = data.map(slot => `<li>${slot.startsAtUtc} - ${slot.endsAtUtc}</li>`).join("");
    status.textContent = data.length === 0 ? "No slots found." : `Loaded ${data.length} slot(s).`;
  } catch (error) {
    status.textContent = error.message;
  }
});

// Ask AI
const askBtn = document.getElementById("askBtn");
const askStatus = document.getElementById("askStatus");
const askResults = document.getElementById("askResults");

askBtn.addEventListener("click", async () => {
  const prompt = document.getElementById("askPrompt").value.trim();
  if (!prompt) {
    askStatus.textContent = "Please enter a prompt.";
    return;
  }

  askStatus.textContent = "Thinking…";
  askResults.innerHTML = "";

  try {
    const response = await fetch("/ask", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ prompt }),
    });

    if (!response.ok) {
      throw new Error(`Request failed with status ${response.status}.`);
    }

    const data = await response.json();
    const items = Array.isArray(data) ? data : (data.slots ?? data.results ?? []);

    if (items.length === 0) {
      askStatus.textContent = "No matching slots found.";
      return;
    }

    askStatus.textContent = `Found ${items.length} option(s).`;
    askResults.innerHTML = items.map(slot => {
      const name = slot.clinicianName ?? slot.clinician ?? "";
      const role = slot.clinicianRole ?? slot.role ?? "";
      const date = slot.date ?? (slot.startsAtUtc ? slot.startsAtUtc.substring(0, 10) : "");
      const time = slot.time ?? (slot.startsAtUtc ? slot.startsAtUtc.substring(11, 16) : "");
      const id = slot.slotId ?? slot.id ?? "";
      return `<li class="slot-card"
          data-slot-id="${id}"
          data-clinician-id="${slot.clinicianId ?? ''}"
          data-starts="${slot.startsAtUtc ?? ''}"
          data-ends="${slot.endsAtUtc ?? ''}"
          data-clinician-name="${name}"
          data-clinician-role="${role}">
        <strong>${name}</strong>${role ? ` &mdash; ${role}` : ""}
        <br />${date} ${time}
        <br /><button class="book-btn" data-slot-id="${id}">Book this</button>
      </li>`;
    }).join("");

    askResults.querySelectorAll(".book-btn").forEach(btn => {
      btn.addEventListener("click", async () => {
        const card = btn.closest("li.slot-card");
        const clinicianId = card.dataset.clinicianId;
        const startsAtUtc = card.dataset.starts;
        const endsAtUtc = card.dataset.ends;
        const clinicianName = card.dataset.clinicianName;
        const clinicianRole = card.dataset.clinicianRole;

        askStatus.textContent = "Booking…";
        try {
          const bookResponse = await fetch("/book", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
              clinicId: "clinic-1",
              clinicianId,
              patientReferenceId: "web-user",
              startsAtUtc,
              endsAtUtc,
            }),
          });
          if (!bookResponse.ok) {
            throw new Error(`Booking failed with status ${bookResponse.status}.`);
          }

          const dateStr = startsAtUtc ? startsAtUtc.substring(0, 10) : "";
          const timeStr = startsAtUtc ? startsAtUtc.substring(11, 16) : "";
          askStatus.textContent = `✓ Appointment booked for ${clinicianName} on ${dateStr} at ${timeStr}.`;

          card.classList.add("booked");
          btn.disabled = true;
          btn.textContent = "Booked ✓";

          const calendarView = document.getElementById("calendarView");
          calendarView.hidden = false;
          calendarView.innerHTML = `<div class="booking-summary">
  <h3>Your Appointment</h3>
  <p><strong>Clinician:</strong> ${clinicianName}${clinicianRole ? ` (${clinicianRole})` : ""}</p>
  <p><strong>Date &amp; Time:</strong> ${dateStr} at ${timeStr}</p>
  <p><strong>Status:</strong> Confirmed</p>
</div>`;
        } catch (bookErr) {
          askStatus.textContent = bookErr.message;
        }
      });
    });
  } catch (error) {
    askStatus.textContent = error.message;
  }
});
