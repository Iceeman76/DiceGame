const storageKey = "dice-game.player-names.v1";

export function load() {
  try {
    return JSON.parse(localStorage.getItem(storageKey) ?? "null");
  } catch {
    return null;
  }
}

export function save(preferences) {
  try {
    localStorage.setItem(storageKey, JSON.stringify(preferences));
  } catch {
  }
}
