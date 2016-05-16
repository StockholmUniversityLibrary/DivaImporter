/*$("#matchResultAccordion").accordion({
    closeAny: true, //true or false. if true other frames (when current opened) will be closed
});*/
$(function () {
    var selector = 'input[type="datetime"]';
    if ($(selector).length) {
        $(selector).datepicker();
    }
    selector = 'input[name="OnlyFirstLastAndLocalAuthors"]';
    if ($(selector).length) {
        toggleAuthorCounts(selector);
    }
    $(selector).on("click", toggleAuthorCounts);
});

function toggleAuthorCounts() {
    var mySelector = 'input[name="OnlyFirstLastAndLocalAuthors"]';
    if ($(mySelector + ":checked").length)
        $('input[name="MaxAuthorCount"]').attr('disabled', '');
    else {
        $('input[name="MaxAuthorCount"]').removeAttr("disabled");
    }
}
$(document).on('change', '.btn-file :file', function () {
    var input = $(this),
        numFiles = input.get(0).files ? input.get(0).files.length : 1,
        label = input.val().replace(/\\/g, '/').replace(/.*\//, '');
    input.trigger('fileselect', [numFiles, label]);
});

$(document).ready(function () {
    $('.btn-file :file').on('fileselect', function (event, numFiles, label) {

        var input = $(this).parents('.input-group').find(':text'),
            log = numFiles > 1 ? numFiles + ' files selected' : label;

        if (input.length) {
            input.val(log);
        } else {
            if (log) alert(log);
        }

    });
});
$(function () {
    $('[data-toggle="popover"]').popover()
})