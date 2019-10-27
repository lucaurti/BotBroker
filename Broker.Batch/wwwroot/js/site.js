// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

Date.prototype.ToDDMMYYYY = function() {
    var mm = this.getMonth() + 1; // getMonth() is zero-based
    var dd = this.getDate();
  
    return [(dd>9 ? '' : '0') + dd, '/',
            (mm>9 ? '' : '0') + mm, '/',
            this.getFullYear()
           ].join('');
};
Date.prototype.ToHHMM = function() {
    var hh = this.getHours();
    var mm = this.getMinutes();
  
    return [(hh>9 ? '' : '0') + hh, ':',
            (mm>9 ? '' : '0') + mm
           ].join('');
};
Date.prototype.ToDateTimeString = function() {
    return [this.ToDDMMYYYY(), this.ToHHMM()].join(' ');
};
