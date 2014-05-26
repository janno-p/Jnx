$(document).ready(function() {
    $('#delete-country').click(function() {
        var $a = $(this);
        if (confirm('Oled kindel, et tahad antud euroala riiki kustutada?')) {
            $.ajax({ url: $a.attr('href'),
                     async: false,
                     method: 'delete',
                     success: function (data) {
                        console.log(data);
                        window.location.href = '/countries';
                     }});
        }
        return false;
    });
});
