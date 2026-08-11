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
