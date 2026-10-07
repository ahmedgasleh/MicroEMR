export function contactFieldName(value, index) {
    return value.replace(/AlternativeContacts\[[^\]]+\]/g, `AlternativeContacts[${index}]`)
        .replace(/alternativeContact-(?:\d+|__index__)-/g, `alternativeContact-${index}-`);
}
for (const editor of document.querySelectorAll("[data-alternative-contacts]")) {
    const list = editor.querySelector("[data-contact-list]");
    const template = editor.querySelector("[data-contact-template]");
    const renumber = () => {
        list.querySelectorAll("[data-alternative-contact]").forEach((card, index) => {
            card.querySelectorAll("[name], [id], [for]").forEach(element => {
                for (const attribute of ["name", "id", "for"]) {
                    const value = element.getAttribute(attribute);
                    if (value)
                        element.setAttribute(attribute, contactFieldName(value, index));
                }
            });
        });
    };
    editor.addEventListener("click", event => {
        const target = event.target;
        if (!(target instanceof Element))
            return;
        if (target.closest("[data-add-contact]")) {
            list.append(template.content.cloneNode(true));
            renumber();
        }
        else if (target.closest("[data-remove-contact]")) {
            target.closest("[data-alternative-contact]")?.remove();
            renumber();
        }
    });
}
//# sourceMappingURL=alternative-contacts.js.map