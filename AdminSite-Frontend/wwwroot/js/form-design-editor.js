window.formDesignEditor = window.formDesignEditor || {};
const isFormDesignElement = value => typeof Element !== "undefined" && value instanceof Element;

window.formDesignEditor.beginWidthDrag = (dotNetRef, startX, startWidth, minimum, maximum, direction, stage) => {
    const controller = new AbortController();
    const clamp = value => Math.max(minimum, Math.min(maximum, Math.round(value)));
    const side = direction < 0 ? -1 : 1;
    let pendingWidth = clamp(startWidth);
    let publishedWidth = pendingWidth;
    let frame = 0;
    let invocation = Promise.resolve();
    const startScrollLeft = isFormDesignElement(stage) ? stage.scrollLeft : 0;

    const publish = () => {
        frame = 0;
        const width = pendingWidth;
        if (width === publishedWidth) return;
        publishedWidth = width;
        invocation = invocation
            .then(() => dotNetRef.invokeMethodAsync("SetDesignWidth", width))
            .then(() => new Promise(resolve => window.requestAnimationFrame(resolve)))
            .then(() => {
                if (side < 0 && isFormDesignElement(stage))
                    stage.scrollLeft = Math.max(0, startScrollLeft + (width - startWidth));
            })
            .catch(() => { });
    };

    const update = clientX => {
        pendingWidth = clamp(startWidth + ((clientX - startX) * side));
    };

    window.addEventListener("pointermove", event => {
        update(event.clientX);
        if (!frame) frame = window.requestAnimationFrame(publish);
    }, { signal: controller.signal });

    const finish = event => {
        if (event?.type === "pointerup" && Number.isFinite(event.clientX))
            update(event.clientX);
        if (frame) window.cancelAnimationFrame(frame);
        publish();
        controller.abort();
    };
    window.addEventListener("pointerup", finish, { once: true, signal: controller.signal });
    window.addEventListener("pointercancel", finish, { once: true, signal: controller.signal });
    window.addEventListener("blur", finish, { once: true, signal: controller.signal });
};

window.formDesignEditor.centerPreview = stage => {
    if (!isFormDesignElement(stage)) return;
    window.requestAnimationFrame(() => window.requestAnimationFrame(() => {
        stage.scrollLeft = Math.max(0, (stage.scrollWidth - stage.clientWidth) / 2);
    }));
};

window.formDesignEditor.destroyDefinitionSortable = container => {
    if (!isFormDesignElement(container) || !container.__formDefinitionSortable) return;
    container.__formDefinitionSortable.destroy();
    delete container.__formDefinitionSortable;
};

window.formDesignEditor.initDefinitionSortable = (container, dotNetRef, enabled) => {
    if (!isFormDesignElement(container)) return;
    window.formDesignEditor.destroyDefinitionSortable(container);
    if (!enabled || !dotNetRef || typeof Sortable === "undefined") return;

    let originalOrder = [];
    const sortable = Sortable.create(container, {
        draggable: ".definition-row-shell",
        dataIdAttr: "data-definition-id",
        animation: 150,
        chosenClass: "form-sortable-chosen",
        ghostClass: "form-sortable-ghost",
        dragClass: "form-sortable-drag",
        onStart: () => {
            originalOrder = sortable.toArray();
            container.classList.add("is-sorting");
        },
        onEnd: event => {
            container.classList.remove("is-sorting");
            if (event.oldIndex === event.newIndex) return;
            const nextOrder = sortable.toArray();
            dotNetRef.invokeMethodAsync("OnDefinitionOrderDropped", nextOrder)
                .then(saved => {
                    if (saved !== false) return;
                    sortable.sort(originalOrder);
                })
                .catch(() => sortable.sort(originalOrder));
        }
    });
    container.__formDefinitionSortable = sortable;
};

window.formDesignEditor.destroyCardSortable = container => {
    if (!isFormDesignElement(container)) return;
    container.__formDesignCardSortable?.destroy();
    delete container.__formDesignCardSortable;
    container.classList.remove("is-sorting-cards");
};

window.formDesignEditor.destroyDesignSortables = (root, informationRoot, auxiliaryRoot) => {
    window.formDesignEditor.destroyCardSortable(informationRoot);
    window.formDesignEditor.destroyCardSortable(auxiliaryRoot);
    if (!isFormDesignElement(root)) return;
    root.__formDesignRowSortable?.destroy();
    (root.__formDesignFieldSortables || []).forEach(sortable => sortable?.destroy());
    root.__formDesignDragController?.abort();
    delete root.__formDesignRowSortable;
    delete root.__formDesignFieldSortables;
    delete root.__formDesignDragController;
    root.classList.remove("field-drag-active", "form-design-rows--drop-after");
    root.querySelectorAll(".form-design-row--drop-before,.form-design-row--drop-rejected")
        .forEach(row => row.classList.remove("form-design-row--drop-before", "form-design-row--drop-rejected"));
};

window.formDesignEditor.initDesignSortables = (root, informationRoot, auxiliaryRoot, dotNetRef, disabled) => {
    window.formDesignEditor.destroyDesignSortables(root, informationRoot, auxiliaryRoot);
    if (disabled || !dotNetRef || typeof Sortable === "undefined") return;

    const initCardSortable = (container, dataIdAttr, method) => {
        if (!isFormDesignElement(container)) return;
        let originalOrder = [];
        const sortable = Sortable.create(container, {
            draggable: ".form-design-item-card",
            dataIdAttr,
            animation: 150,
            filter: "button,input,label,a,select,textarea",
            preventOnFilter: false,
            chosenClass: "form-sortable-chosen",
            ghostClass: "form-sortable-ghost",
            dragClass: "form-sortable-drag",
            onStart: () => {
                originalOrder = sortable.toArray();
                container.classList.add("is-sorting-cards");
            },
            onEnd: event => {
                container.classList.remove("is-sorting-cards");
                if (event.oldIndex === event.newIndex) return;
                dotNetRef.invokeMethodAsync(method, sortable.toArray())
                    .then(saved => {
                        if (saved !== false) return;
                        sortable.sort(originalOrder);
                    })
                    .catch(() => sortable.sort(originalOrder));
            }
        });
        container.__formDesignCardSortable = sortable;
    };

    initCardSortable(informationRoot, "data-info-id", "OnInformationItemsReordered");
    initCardSortable(auxiliaryRoot, "data-action-id", "OnAuxiliaryActionsReordered");

    if (!isFormDesignElement(root)) return;

    const controller = new AbortController();
    const fieldSortables = [];
    let activeField = null;
    let originalRowOrder = [];

    const clearFieldIndicator = () => {
        root.classList.remove("form-design-rows--drop-after");
        root.querySelectorAll(".form-design-row--drop-before,.form-design-row--drop-rejected")
            .forEach(row => row.classList.remove("form-design-row--drop-before", "form-design-row--drop-rejected"));
    };

    const updateFieldDrop = (clientX, clientY) => {
        if (!activeField || !Number.isFinite(clientX) || !Number.isFinite(clientY)) return;
        clearFieldIndicator();
        activeField.outside = false;
        activeField.rejected = false;
        activeField.insertionIndex = -1;

        const sourceRect = activeField.source.getBoundingClientRect();
        if (clientX >= sourceRect.left && clientX <= sourceRect.right && clientY >= sourceRect.top && clientY <= sourceRect.bottom)
            return;

        activeField.outside = true;
        const pointedFields = document.elementFromPoint(clientX, clientY)?.closest(".form-design-row__fields");
        if (pointedFields && pointedFields !== activeField.source) {
            activeField.rejected = true;
            pointedFields.closest(".form-design-row")?.classList.add("form-design-row--drop-rejected");
            return;
        }

        const rows = [...root.querySelectorAll(":scope > .form-design-row")];
        const insertionIndex = rows.findIndex(row => clientY < row.getBoundingClientRect().top + (row.getBoundingClientRect().height / 2));
        activeField.insertionIndex = insertionIndex < 0 ? rows.length : insertionIndex;
        if (activeField.insertionIndex >= rows.length) root.classList.add("form-design-rows--drop-after");
        else rows[activeField.insertionIndex].classList.add("form-design-row--drop-before");
    };

    window.addEventListener("pointermove", event => updateFieldDrop(event.clientX, event.clientY), { signal: controller.signal });
    window.addEventListener("dragover", event => updateFieldDrop(event.clientX, event.clientY), { signal: controller.signal });

    const rowSortable = Sortable.create(root, {
        draggable: ".form-design-row",
        dataIdAttr: "data-row-id",
        animation: 150,
        filter: ".form-design-row__fields,button,input,label,a,select,textarea",
        preventOnFilter: false,
        chosenClass: "form-sortable-chosen",
        ghostClass: "form-sortable-ghost",
        dragClass: "form-sortable-drag",
        onStart: () => {
            originalRowOrder = rowSortable.toArray();
            root.classList.add("is-sorting-rows");
        },
        onEnd: event => {
            root.classList.remove("is-sorting-rows");
            if (event.oldIndex === event.newIndex) return;
            const nextOrder = rowSortable.toArray();
            dotNetRef.invokeMethodAsync("OnRowsReordered", nextOrder)
                .then(saved => {
                    if (saved !== false) return;
                    rowSortable.sort(originalRowOrder);
                })
                .catch(() => rowSortable.sort(originalRowOrder));
        }
    });

    root.querySelectorAll(":scope > .form-design-row > .form-design-row__fields").forEach(fieldContainer => {
        let originalFields = [];
        const fieldSortable = Sortable.create(fieldContainer, {
            draggable: ".form-design-field-chip",
            dataIdAttr: "data-field-key",
            direction: "horizontal",
            animation: 150,
            filter: "input,button,a,select,textarea",
            preventOnFilter: false,
            group: { name: "form-design-field-chips", pull: true, put: false },
            chosenClass: "form-sortable-chosen",
            ghostClass: "form-sortable-ghost",
            dragClass: "form-sortable-drag",
            onStart: event => {
                originalFields = fieldSortable.toArray();
                activeField = {
                    source: fieldContainer,
                    fieldKey: event.item?.dataset.fieldKey || "",
                    outside: false,
                    rejected: false,
                    insertionIndex: -1
                };
                root.classList.add("field-drag-active");
            },
            onEnd: event => {
                if (event.originalEvent)
                    updateFieldDrop(event.originalEvent.clientX, event.originalEvent.clientY);
                const completed = activeField;
                activeField = null;
                root.classList.remove("field-drag-active");
                clearFieldIndicator();

                if (!completed) return;
                if (completed.outside) {
                    fieldSortable.sort(originalFields);
                    if (completed.rejected || completed.insertionIndex < 0) return;
                    dotNetRef.invokeMethodAsync("OnFieldMovedToNewRow", completed.fieldKey, completed.insertionIndex)
                        .catch(() => fieldSortable.sort(originalFields));
                    return;
                }

                if (event.oldIndex === event.newIndex) return;
                const rowId = fieldContainer.dataset.rowId || "";
                const nextFields = fieldSortable.toArray();
                dotNetRef.invokeMethodAsync("OnFieldsReordered", rowId, nextFields)
                    .then(saved => {
                        if (saved !== false) return;
                        fieldSortable.sort(originalFields);
                    })
                    .catch(() => fieldSortable.sort(originalFields));
            }
        });
        fieldSortables.push(fieldSortable);
    });

    root.__formDesignRowSortable = rowSortable;
    root.__formDesignFieldSortables = fieldSortables;
    root.__formDesignDragController = controller;
};

window.formDesignEditor.beginSplitDrag = (dotNetRef, startX, startPercent, designWidth, minimum, maximum) => {
    const controller = new AbortController();
    const width = Math.max(1, Number(designWidth) || 1);
    const clamp = value => Math.max(minimum, Math.min(maximum, Math.round(value)));
    let pendingPercent = clamp(startPercent);
    let frame = 0;

    const publish = () => {
        frame = 0;
        dotNetRef.invokeMethodAsync("SetSplitPanelPercent", pendingPercent).catch(() => { });
    };

    window.addEventListener("pointermove", event => {
        pendingPercent = clamp(startPercent + ((event.clientX - startX) / width) * 100);
        if (!frame) frame = window.requestAnimationFrame(publish);
    }, { signal: controller.signal });

    const finish = event => {
        if (event?.type === "pointerup" && Number.isFinite(event.clientX))
            pendingPercent = clamp(startPercent + ((event.clientX - startX) / width) * 100);
        if (frame) window.cancelAnimationFrame(frame);
        publish();
        controller.abort();
    };
    window.addEventListener("pointerup", finish, { once: true, signal: controller.signal });
    window.addEventListener("pointercancel", finish, { once: true, signal: controller.signal });
};

window.formDesignEditor.openDrawer = (drawer, dotNetRef) => {
    if (!isFormDesignElement(drawer)) return;
    window.formDesignEditor.closeDrawer(drawer);

    const previousFocus = document.activeElement;
    const controller = new AbortController();
    const focusableSelector = [
        "button:not([disabled])",
        "input:not([disabled])",
        "select:not([disabled])",
        "textarea:not([disabled])",
        "a[href]",
        "[tabindex]:not([tabindex='-1'])"
    ].join(",");

    const focusable = () => Array.from(drawer.querySelectorAll(focusableSelector))
        .filter(element => element.offsetParent !== null);

    drawer.addEventListener("keydown", event => {
        if (event.key === "Escape") {
            event.preventDefault();
            dotNetRef.invokeMethodAsync("RequestCloseDesignDrawer").catch(() => { });
        }
    }, { signal: controller.signal });

    drawer.__formDesignDrawer = { controller, previousFocus };
    window.requestAnimationFrame(() => {
        const first = focusable()[0];
        if (first) first.focus();
        else {
            drawer.tabIndex = -1;
            drawer.focus();
        }
    });
};

window.formDesignEditor.closeDrawer = drawer => {
    if (!isFormDesignElement(drawer)) return;
    const state = drawer?.__formDesignDrawer;
    if (!state) return;
    state.controller.abort();
    delete drawer.__formDesignDrawer;
    if (state.previousFocus?.isConnected && typeof state.previousFocus.focus === "function")
        state.previousFocus.focus();
};
