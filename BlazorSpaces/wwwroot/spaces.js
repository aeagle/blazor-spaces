if (!spaces_updateStyleDefinition) {
  window.spaces_updateStyleDefinition = function (id, definition) {
    const existing = document.getElementById(`style_${id}`);

    if (existing) {
      if (existing.innerHTML !== definition) {
        existing.innerHTML = definition;
      }
    } else {
      const newStyle = document.createElement("style");
      newStyle.id = `style_${id}`;
      newStyle.innerHTML = definition;
      document.head.appendChild(newStyle);
    }
  };
}

if (!spaces_updateStyleDefinition) {
  window.spaces_removeStyleDefinition = function (id) {
    const existing = document.getElementById(`style_${id}`);
    if (existing) {
      document.head.removeChild(existing);
    }
  };
}
