let rawInventoryData = [];

function renderItems(itemsToRender) {
  const container = document.getElementById("inventory");
  const status = document.getElementById("status");

  if (status) status.innerText = `Найдено предметов: ${itemsToRender.length}`;
  if (!container) return;

  container.innerHTML = "";

  itemsToRender.forEach(item => {
    const card = document.createElement("div");
    card.className = "card";

    const badgeClass = item.tradable ? "tradable" : "untradable";
    const badgeText = item.tradable ? "Можно продать/передать." : "Блокировка обмена/продажи.";

    const nameColor = item.color ? `#${item.color}` : (item.rarityColor ? `#${item.rarityColor}` : "#e3faf8");

    card.innerHTML = `
      <img src="${item.iconUrl}" alt="${item.name}">
      <p style="color: ${nameColor}; font-weight: bold;">${item.name}</p>
      <div>
        <span class="badge ${badgeClass}">${badgeText}</span>
      </div>
    `;

    container.appendChild(card);
  });
}

function populateRarityOptions(items) {
  const filterSelect = document.getElementById("rarityFilter");
  if (!filterSelect) return;

  filterSelect.innerHTML = '<option value="all">Все предметы</option>';
  const rarities = new Set();

  items.forEach(item => {
    if (item.rarity) rarities.add(item.rarity);
  });

  rarities.forEach(rarity => {
    const option = document.createElement("option");
    option.value = rarity;
    option.textContent = rarity;
    filterSelect.appendChild(option);
  });
}

function applyRarityFilter() {
  const selectedRarity = document.getElementById("rarityFilter").value;

  if (selectedRarity === "all") {
    renderItems(rawInventoryData);
  } else {
    const filtered = rawInventoryData.filter(item => item.rarity === selectedRarity);
    renderItems(filtered);
  }
}

document.addEventListener("DOMContentLoaded", () => {
  const loadBtn = document.getElementById("loadBtn");
  const steamInput = document.getElementById("steamInput");
  const rarityFilter = document.getElementById("rarityFilter");

  if (loadBtn) {
    loadBtn.addEventListener("click", loadInventory);
  }

  if (steamInput) {
    steamInput.addEventListener("keypress", (e) => {
      if (e.key === "Enter") loadInventory();
    });
  }

  if (rarityFilter) {
    rarityFilter.addEventListener("change", applyRarityFilter);
  }
});

async function loadInventory() {
  const input = document.getElementById("steamInput").value.trim();
  const status = document.getElementById("status");
  const container = document.getElementById("inventory");

  if (!input) {
    alert("Введите ссылку на профиль Steam.");
    return;
  }

  if (status) status.innerText = "Загрузка инвентаря...";
  if (container) container.innerHTML = "";

  try {
    const response = await fetch(`/api/steam?input=${encodeURIComponent(input)}`);
    const data = await response.json();

    if (!response.ok) {
      if (status) status.innerText = data.detail || "При загрузке инвентаря произошла ошибка";
      return;
    }

    if (data.length === 0) {
      if (status) status.innerText = "Инвентарь пуст или скрыт настройками приватности.";
      return;
    }

    const seenIds = new Set();
    rawInventoryData = data.filter(item => {
      const uniqueKey = item.assetId || item.id || `${item.name}_${item.iconUrl}`;
      if (seenIds.has(uniqueKey)) {
        return false;
      }
      seenIds.add(uniqueKey);
      return true;
    });

    populateRarityOptions(rawInventoryData);
    renderItems(rawInventoryData);

  } catch (err) {
    console.error(err);
    if (status) status.innerText = "Ошибка соединения с сервером";
  }
}