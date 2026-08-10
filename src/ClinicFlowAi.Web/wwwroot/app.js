const slots = document.getElementById("slots");
document.getElementById("load").addEventListener("click", async () => {
  const clinicId = document.getElementById("clinicId").value;
  const clinicianId = document.getElementById("clinicianId").value;
  const url = `/availability?ClinicId=${encodeURIComponent(clinicId)}&ClinicianId=${encodeURIComponent(clinicianId)}&WindowStartUtc=2026-08-11T00:00:00Z&WindowEndUtc=2026-08-12T00:00:00Z&AppointmentTypeCode=exam`;
  const response = await fetch(url);
  const data = await response.json();
  slots.innerHTML = data.map(slot => `<li>${slot.startsAtUtc} - ${slot.endsAtUtc}</li>`).join("");
});
