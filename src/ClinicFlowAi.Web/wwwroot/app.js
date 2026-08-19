const slots = document.getElementById("slots");
const status = document.getElementById("status");
const askPrompt = document.getElementById("askPrompt");
const recentPromptList = document.getElementById("recentPromptList");
const clearPromptHistory = document.getElementById("clearPromptHistory");
const promptHistoryKey = "clinicflow-ai.recentPrompts";
const maxPromptHistory = 5;

function loadPromptHistory() {
  try {
    const raw = localStorage.getItem(promptHistoryKey);
    const prompts = raw ? JSON.parse(raw) : [];
    return Array.isArray(prompts) ? prompts.filter(prompt => typeof prompt === "string" && prompt.trim()) : [];
  } catch {
    return [];
  }
}

function savePromptHistory(prompts) {
  const uniquePrompts = prompts.slice(0, maxPromptHistory);
  try {
    localStorage.setItem(promptHistoryKey, JSON.stringify(uniquePrompts));
  } catch {
    // Ignore storage failures so the booking flow still works.
  }
}

function renderPromptHistory() {
  if (!recentPromptList) {
    return;
  }

  const prompts = loadPromptHistory();
  if (prompts.length === 0) {
    recentPromptList.innerHTML = '<span class="prompt-empty">No recent prompts yet.</span>';
    return;
  }

  recentPromptList.innerHTML = prompts.map(prompt => (
    `<button type="button" class="prompt-chip" data-prompt="${escapeHtml(prompt)}">${escapeHtml(prompt)}</button>`
  )).join("");

  recentPromptList.querySelectorAll(".prompt-chip").forEach(button => {
    button.addEventListener("click", () => {
      askPrompt.value = button.dataset.prompt ?? "";
      askPrompt.focus();
    });
  });
}

function rememberPrompt(prompt) {
  const prompts = loadPromptHistory().filter(item => item !== prompt);
  prompts.unshift(prompt);
  savePromptHistory(prompts);
  renderPromptHistory();
}

function escapeHtml(value) {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

renderPromptHistory();

if (clearPromptHistory) {
  clearPromptHistory.addEventListener("click", () => {
    localStorage.removeItem(promptHistoryKey);
    renderPromptHistory();
  });
}

function isoNow() {
  return new Date().toISOString();
}

function isoPlusDays(days) {
  const d = new Date();
  d.setDate(d.getDate() + days);
  return d.toISOString();
}

document.getElementById("load").addEventListener("click", async () => {
  const clinicId = document.getElementById("clinicId").value;
  const clinicianId = document.getElementById("clinicianId").value;
  const typeCode = document.getElementById("appointmentTypeCode").value || "exam";
  const start = isoNow();
  const end = isoPlusDays(14);
  const url = `/availability?ClinicId=${encodeURIComponent(clinicId)}&ClinicianId=${encodeURIComponent(clinicianId)}&WindowStartUtc=${encodeURIComponent(start)}&WindowEndUtc=${encodeURIComponent(end)}&AppointmentTypeCode=${encodeURIComponent(typeCode)}`;

  status.textContent = "Loading availability...";
  slots.innerHTML = "";

  try {
    const response = await fetch(url);
    if (!response.ok) {
      throw new Error(`Availability request failed with status ${response.status}.`);
    }

    const data = await response.json();
    if (data.length === 0) {
      status.textContent = "No slots found.";
      return;
    }
    status.textContent = `Loaded ${data.length} slot(s).`;
    slots.innerHTML = data.map(slot => {
      const dateStr = slot.startsAtUtc ? slot.startsAtUtc.substring(0, 10) : "";
      const timeStr = slot.startsAtUtc ? slot.startsAtUtc.substring(11, 16) : "";
      return `<li class="slot-card"
          data-clinician-id="${encodeURIComponent(clinicianId)}"
          data-clinic-id="${encodeURIComponent(clinicId)}"
          data-starts="${slot.startsAtUtc ?? ''}"
          data-ends="${slot.endsAtUtc ?? ''}">
        <strong>${dateStr}</strong> at ${timeStr}
        <br /><button class="book-btn">Book this</button>
      </li>`;
    }).join("");

    slots.querySelectorAll(".book-btn").forEach(btn => {
      btn.addEventListener("click", () => bookSlot(btn, clinicianId, clinicId));
    });
  } catch (error) {
    status.textContent = error.message;
  }
});

async function bookSlot(btn, clinicianId, clinicId) {
  const card = btn.closest("li.slot-card");
  const startsAtUtc = card.dataset.starts;
  const endsAtUtc = card.dataset.ends;
  const dateStr = startsAtUtc ? startsAtUtc.substring(0, 10) : "";
  const timeStr = startsAtUtc ? startsAtUtc.substring(11, 16) : "";

  status.textContent = "Booking...";
  try {
    const bookResponse = await fetch("/book", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        clinicId,
        clinicianId,
        patientReferenceId: "web-user",
        startsAtUtc,
        endsAtUtc,
      }),
    });
    if (!bookResponse.ok) {
      throw new Error(`Booking failed with status ${bookResponse.status}.`);
    }
    status.textContent = `\u2713 Appointment booked on ${dateStr} at ${timeStr}.`;
    card.classList.add("booked");
    btn.disabled = true;
    btn.textContent = "Booked \u2713";
    showCalendar(clinicianId, dateStr, timeStr);
  } catch (err) {
    status.textContent = err.message;
  }
}

// Ask AI
const askBtn = document.getElementById("askBtn");
const askStatus = document.getElementById("askStatus");
const askResults = document.getElementById("askResults");

askBtn.addEventListener("click", async () => {
  const prompt = askPrompt.value.trim();
  if (!prompt) {
    askStatus.textContent = "Please enter a prompt.";
    return;
  }

  askStatus.textContent = "Thinking\u2026";
  askResults.innerHTML = "";

  try {
    const response = await fetch("/ask", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ prompt, maxResults: 5 }),
    });

    if (!response.ok) {
      throw new Error(`Request failed with status ${response.status}.`);
    }

    const data = await response.json();
    const items = Array.isArray(data) ? data : (data.slots ?? data.results ?? []);
    rememberPrompt(prompt);

    if (items.length === 0) {
      askStatus.textContent = "No matching slots found.";
      return;
    }

    askStatus.textContent = `Found ${items.length} option(s). Click a slot to book.`;
    askResults.innerHTML = items.map(slot => {
      const name = slot.clinicianName ?? slot.clinician ?? "";
      const role = slot.clinicianRole ?? slot.role ?? "";
      const dateStr = slot.startsAtUtc ? slot.startsAtUtc.substring(0, 10) : "";
      const timeStr = slot.startsAtUtc ? slot.startsAtUtc.substring(11, 16) : "";
      const clinicianId = slot.clinicianId ?? "";
      return `<li class="slot-card"
          data-clinician-id="${clinicianId}"
          data-clinic-id="clinic-1"
          data-starts="${slot.startsAtUtc ?? ''}"
          data-ends="${slot.endsAtUtc ?? ''}"
          data-clinician-name="${name}"
          data-clinician-role="${role}">
        <strong>${name}</strong>${role ? ` &mdash; ${role}` : ""}
        <br />${dateStr} at ${timeStr}
        <br /><button class="book-btn">Book this</button>
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
        const dateStr = startsAtUtc ? startsAtUtc.substring(0, 10) : "";
        const timeStr = startsAtUtc ? startsAtUtc.substring(11, 16) : "";

        askStatus.textContent = "Booking\u2026";
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

          askStatus.textContent = `\u2713 Appointment booked with ${clinicianName} on ${dateStr} at ${timeStr}.`;
          card.classList.add("booked");
          btn.disabled = true;
          btn.textContent = "Booked \u2713";
          showCalendar(clinicianName || clinicianRole, dateStr, timeStr);
        } catch (bookErr) {
          askStatus.textContent = bookErr.message;
        }
      });
    });
  } catch (error) {
    askStatus.textContent = error.message;
  }
});

function showCalendar(clinician, dateStr, timeStr) {
  const calendarView = document.getElementById("calendarView");
  calendarView.hidden = false;
  calendarView.innerHTML = `<div class="booking-summary">
  <h3>\u2705 Your Appointment</h3>
  <p><strong>Clinician:</strong> ${clinician}</p>
  <p><strong>Date &amp; Time:</strong> ${dateStr} at ${timeStr}</p>
  <p><strong>Status:</strong> Confirmed</p>
</div>`;
  calendarView.scrollIntoView({ behavior: "smooth" });
}
